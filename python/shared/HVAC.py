import numpy as np
import pandas as pd
from scipy.optimize import minimize

from shared.MongoDbController import MongoDbController


class HVAC:
    def __init__(self, num_nodes, max_heating_powers, district_id_dict, is_digital_twin):
        self.horizon_hours = 6
        self.block_size = 12

        self.cached_plan = None
        self.plan_step_index = 0

        self.mongodb = MongoDbController()

        self.num_nodes = num_nodes
        self.max_powers = max_heating_powers
        self.district_id_dict = district_id_dict
        self.is_digital_twin = is_digital_twin

        self.target_24h = None
        self.min_24h = None
        self.max_24h = None
        self.is_enabled_24h = None
        self.set_temperatures_config()

    def set_temperatures_config(self):
        self.target_24h = np.full((self.num_nodes, 24), 21.0)
        tolerance_channel = np.full((self.num_nodes, 24), 0.5)
        self.is_enabled_24h = np.zeros((self.num_nodes, 24))

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
                    self.target_24h[idx, :] = temps

                tolerance_channel[idx] = ctrl.get("Tolerance", 0.5)

                is_enabled = ctrl.get("IsEnabled")
                if is_enabled and len(is_enabled) == 24:
                    self.is_enabled_24h[idx, :] = [1.0 if s else 0.0 for s in is_enabled]

        self.min_24h = self.target_24h - tolerance_channel
        self.max_24h = self.target_24h + tolerance_channel

    def get_target_trajectories(self, current_time, dt, horizon_steps):
        T_min_horizon = np.zeros((horizon_steps, self.num_nodes))
        T_max_horizon = np.zeros((horizon_steps, self.num_nodes))

        future_time = current_time
        for k in range(horizon_steps):
            time_float = future_time.hour + (future_time.minute / 60.0)
            h0 = int(time_float) % 24
            h1 = (h0 + 1) % 24
            w = time_float - int(time_float)
            mu = (1.0 - np.cos(w * np.pi)) / 2.0

            base_min = self.min_24h[:, h0] * (1.0 - mu) + self.min_24h[:, h1] * mu
            base_max = self.max_24h[:, h0] * (1.0 - mu) + self.max_24h[:, h1] * mu

            is_active = self.is_enabled_24h[:, h0] > 0.5

            T_min_horizon[k, :] = np.where(is_active, base_min, 16.0)
            T_max_horizon[k, :] = np.where(is_active, base_max, 26.0)

            future_time += pd.Timedelta(seconds=dt)

        return T_min_horizon, T_max_horizon

    def cost_function(self, q_hvac_flat, current_T, T_min_hor, T_max_hor, t_out_for, q_env_for, thermal_solver, dt,
                      horizon_steps):
        Q_hvac_blocked = q_hvac_flat.reshape((-1, self.num_nodes))
        Q_hvac_matrix = np.repeat(Q_hvac_blocked, self.block_size, axis=0)

        T_sim = np.copy(current_T)
        total_penalty = 0.0

        G = thermal_solver.G
        C = thermal_solver.C
        G_ext_air = thermal_solver.G_ext_air
        G_ext_ground = thermal_solver.G_ext_ground
        T_ground = thermal_solver.T_ground

        for k in range(horizon_steps):
            Q_hvac = Q_hvac_matrix[k]

            Q_inter = np.dot(G, T_sim) - (np.sum(G, axis=1) * T_sim)
            Q_air = G_ext_air * (t_out_for[k] - T_sim)
            Q_ground = G_ext_ground * (T_ground - T_sim)

            total_Q = Q_inter + Q_air + Q_ground + q_env_for[k] + Q_hvac
            T_sim += (total_Q / C) * dt

            below_min = np.maximum(0, T_min_hor[k] - T_sim)
            above_max = np.maximum(0, T_sim - T_max_hor[k])

            total_penalty += np.sum(below_min) * 10000.0
            total_penalty += np.sum(above_max) * 10000.0

            total_penalty += np.sum(below_min ** 2) * 50000.0
            total_penalty += np.sum(above_max ** 2) * 50000.0

            delta_q = np.diff(Q_hvac_blocked, axis=0)

            delta_percent = delta_q / self.max_powers

            total_penalty += np.sum(delta_percent ** 2) * 5000.0

            delta_q = np.abs(np.diff(Q_hvac_blocked, axis=0))
            delta_percent = delta_q / (self.max_powers + 1e-9)

            illegal_jumps = np.maximum(0, delta_percent - 0.15)

            total_penalty += np.sum(illegal_jumps) * 10000000.0

        return total_penalty

    def step(self, current_time, dt, thermal_solver, weather_service, weather_solver):
        if dt == 0:
            return np.zeros(self.num_nodes)

        horizon_steps = int((self.horizon_hours * 3600) / dt)

        needs_recalc = False

        if self.cached_plan is None:
            needs_recalc = True
        elif not self.is_digital_twin and self.plan_step_index >= 12:
            needs_recalc = True

        if needs_recalc:
            t_out_forecast = np.zeros(horizon_steps)
            q_env_forecast = np.zeros((horizon_steps, self.num_nodes))
            future_time = current_time
            T_frozen_for_prediction = np.copy(thermal_solver.T)

            for k in range(horizon_steps):
                w = weather_service.get_weather(future_time)
                t_out_forecast[k] = w["temperature"]
                q_env = weather_solver.calculate_environmental_gains(
                    w["sun_radiation"], w["sun_altitude"], w["sun_azimuth"],
                    w["wind_speed"], w["wind_direction"], w["temperature"],
                    T_frozen_for_prediction
                )
                q_env_forecast[k, :] = q_env
                future_time += pd.Timedelta(seconds=dt)

            T_min_hor, T_max_hor = self.get_target_trajectories(current_time, dt, horizon_steps)

            control_steps = horizon_steps // self.block_size

            bounds = [(0.0, self.max_powers[i]) for _ in range(control_steps) for i in range(self.num_nodes)]
            initial_guess = np.zeros(control_steps * self.num_nodes)

            res = minimize(
                self.cost_function,
                initial_guess,
                args=(thermal_solver.T, T_min_hor, T_max_hor, t_out_forecast, q_env_forecast, thermal_solver, dt,
                      horizon_steps),
                method='L-BFGS-B',
                bounds=bounds,
                options={
                    'maxiter': 20,
                    'ftol': 1e-3,
                    'eps': 100.0,
                    'disp': False
                }
            )

            optimal_plan_blocked = res.x.reshape((control_steps, self.num_nodes))

            self.cached_plan = np.repeat(optimal_plan_blocked, self.block_size, axis=0)
            self.plan_step_index = 0

        if self.plan_step_index < len(self.cached_plan):
            current_optimal_q = self.cached_plan[self.plan_step_index, :]
        else:
            current_optimal_q = np.zeros(self.num_nodes)

        self.plan_step_index += 1

        return current_optimal_q

    # def step(self, current_time, dt, thermal_solver, weather_service, weather_solver):
    #     if dt == 0:
    #         return np.zeros(self.num_nodes)
    #
    #     horizon_steps = int((self.horizon_hours * 3600) / dt)
    #
    #     needs_recalc = False
    #
    #     if self.cached_plan is None:
    #         needs_recalc = True
    #     elif not self.is_digital_twin and self.plan_step_index >= 12:
    #         needs_recalc = True
    #
    #     if needs_recalc:
    #         t_out_forecast = np.zeros(horizon_steps)
    #         q_env_forecast = np.zeros((horizon_steps, self.num_nodes))
    #         future_time = current_time
    #         T_frozen_for_prediction = np.copy(thermal_solver.T)
    #
    #         for k in range(horizon_steps):
    #             w = weather_service.get_weather(future_time)
    #             t_out_forecast[k] = w["temperature"]
    #             q_env = weather_solver.calculate_environmental_gains(
    #                 w["sun_radiation"], w["sun_altitude"], w["sun_azimuth"],
    #                 w["wind_speed"], w["wind_direction"], w["temperature"],
    #                 T_frozen_for_prediction
    #             )
    #             q_env_forecast[k, :] = q_env
    #             future_time += pd.Timedelta(seconds=dt)
    #
    #         T_min_hor, T_max_hor = self.get_target_trajectories(current_time, dt, horizon_steps)
    #
    #         control_steps = horizon_steps // self.block_size
    #
    #         initial_guess = np.tile(self.max_powers / 2.0, control_steps)
    #         bounds = [(0.0, self.max_powers[i]) for _ in range(control_steps) for i in range(self.num_nodes)]
    #
    #         res = minimize(
    #             self.cost_function,
    #             initial_guess,
    #             args=(thermal_solver.T, T_min_hor, T_max_hor, t_out_forecast, q_env_forecast, thermal_solver, dt,
    #                   horizon_steps),
    #             method='L-BFGS-B',
    #             bounds=bounds,
    #             options={
    #                 'maxiter': 20,
    #                 'ftol': 1e-3,
    #                 'eps': 100.0,
    #                 'disp': False
    #             }
    #         )
    #
    #         optimal_plan_blocked = res.x.reshape((control_steps, self.num_nodes))
    #
    #         self.cached_plan = np.repeat(optimal_plan_blocked, self.block_size, axis=0)
    #         self.plan_step_index = 0
    #
    #     if self.plan_step_index < len(self.cached_plan):
    #         current_optimal_q = self.cached_plan[self.plan_step_index, :] * self.enabled_mask
    #     else:
    #         current_optimal_q = np.zeros(self.num_nodes)
    #
    #     self.plan_step_index += 1
    #
    #     return current_optimal_q
