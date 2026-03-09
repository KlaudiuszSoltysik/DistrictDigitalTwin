import numpy as np

from shared.MongoDbController import MongoDbController


class HVAC:
    def __init__(self, num_nodes, max_heating_powers, district_id_dict):
        self.mongodb = MongoDbController()

        self.num_nodes = num_nodes
        self.max_powers = max_heating_powers
        self.district_id_dict = district_id_dict

        self.setpoints_24h = None
        self.tolerance_channel = None
        self.enabled_mask = None
        self.set_temperatures_config()

        self.integral_error = np.zeros(num_nodes)
        self.current_q = np.zeros(num_nodes)
        self.k_p = None
        self.k_i = None
        self.set_unit_config()

        # TODO: tweak
        self.tau = 4.0 * 3600.0  # 1-8

    def set_unit_config(self):
        hvac_config = self.mongodb.get_device("hvac")

        p_band = hvac_config["PBand"]  # 1-4
        self.k_p = self.max_powers / p_band

        t_i = hvac_config["Ti"]  # 1-8
        self.k_i = self.k_p / t_i

    def set_temperatures_config(self):
        self.setpoints_24h = np.full((self.num_nodes, 24), 21.0)
        self.tolerance_channel = np.full(self.num_nodes, 0.5)
        self.enabled_mask = np.zeros(self.num_nodes)

        configs = list(self.mongodb.db["apartments-config"].find({}))

        mongo_map = {}
        for apt in configs:
            b_id = apt.get("BuildingId")
            a_id = apt.get("ApartmentId")

            for room in apt.get("Rooms", []):
                r_id = room.get("_id")
                flat_key = f"{b_id}:{a_id}:{r_id}"
                mongo_map[flat_key] = room.get("HvacControl", {})

        for idx, full_id in self.district_id_dict.items():
            if full_id in mongo_map:
                ctrl = mongo_map[full_id]

                temps = ctrl.get("Temperatures")
                if temps and len(temps) == 24:
                    self.setpoints_24h[idx, :] = temps

                self.tolerance_channel[idx] = ctrl.get("Tolerance", 0.5)
                self.enabled_mask[idx] = 1.0 if ctrl.get("IsEnabled", True) else 0.0

    def get_current_control_state(self, current_time):
        time_float = current_time.hour + (current_time.minute / 60.0) + (current_time.second / 3600.0)

        h0 = int(time_float) % 24
        h1 = (h0 + 1) % 24
        w = time_float - int(time_float)

        mu = (1.0 - np.cos(w * np.pi)) / 2.0

        return self.setpoints_24h[:, h0] * (1.0 - mu) + self.setpoints_24h[:, h1] * mu

    def step(self, current_time, dt, T):
        T_target = self.get_current_control_state(current_time)

        error = T_target - T

        in_deadband = (T >= (T_target - self.tolerance_channel)) & (T <= T_target)
        error = np.where(in_deadband, 0.0, error)

        p_term = error * self.k_p

        self.integral_error += error * dt

        max_integral = self.max_powers / self.k_i
        self.integral_error = np.clip(self.integral_error, 0.0, max_integral)

        i_term = self.integral_error * self.k_i

        q_target = p_term + i_term

        q_target = np.clip(q_target, 0.0, self.max_powers)

        q_target = q_target * self.enabled_mask

        alpha = dt / self.tau
        alpha = min(alpha, 1.0)
        self.current_q += alpha * (q_target - self.current_q)

        return self.current_q
