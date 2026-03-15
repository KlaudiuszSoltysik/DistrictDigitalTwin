import numpy as np
import pandas as pd
from scipy.optimize import minimize

from shared.MongoDbController import MongoDbController


class HVAC:
    def __init__(self, num_nodes, max_heating_powers, district_id_dict):
        self.horizon_hours = 6

        self.mongodb = MongoDbController()

        self.num_nodes = num_nodes
        self.max_powers = max_heating_powers
        self.district_id_dict = district_id_dict

        self.target_24h = None
        self.min_24h = None
        self.max_24h = None
        self.enabled_mask = None
        self.set_temperatures_config()

    def set_temperatures_config(self):
        self.target_24h = np.full((self.num_nodes, 24), 21.0)
        tolerance_channel = np.full((self.num_nodes, 24), 0.5)
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
                    self.target_24h[idx, :] = temps

                tolerance_channel[idx] = ctrl.get("Tolerance", 0.5)
                self.enabled_mask[idx] = 1.0 if ctrl.get("IsEnabled", True) else 0.0

        self.min_24h = self.target_24h - tolerance_channel
        self.max_24h = self.target_24h + tolerance_channel

    def _get_target_trajectories(self, current_time, dt, horizon_steps):
        """
        Mapuje dobowe ustawienia z Mongo na precyzyjny wektor dla okna predykcji.
        Używa interpolacji kosinusoidalnej (jak w starym PID), żeby tunel był gładki.
        """
        T_min_horizon = np.zeros((horizon_steps, self.num_nodes))
        T_max_horizon = np.zeros((horizon_steps, self.num_nodes))

        future_time = current_time
        for k in range(horizon_steps):
            time_float = future_time.hour + (future_time.minute / 60.0)
            h0 = int(time_float) % 24
            h1 = (h0 + 1) % 24
            w = time_float - int(time_float)
            mu = (1.0 - np.cos(w * np.pi)) / 2.0

            T_min_horizon[k, :] = self.min_24h[:, h0] * (1.0 - mu) + self.min_24h[:, h1] * mu
            T_max_horizon[k, :] = self.max_24h[:, h0] * (1.0 - mu) + self.max_24h[:, h1] * mu

            future_time += pd.Timedelta(seconds=dt)

        return T_min_horizon, T_max_horizon

    def _cost_function(self, q_hvac_flat, current_T, T_min_hor, T_max_hor, t_out_for, q_env_for, thermal_solver, dt,
                       horizon_steps, block_size):
        """
        Symuluje budynek dla podanego planu mocy i zwraca 'karę'. Im mniejsza liczba, tym lepszy plan.
        """
        Q_hvac_blocked = q_hvac_flat.reshape((-1, self.num_nodes))
        Q_hvac_matrix = np.repeat(Q_hvac_blocked, block_size, axis=0)

        T_sim = np.copy(current_T)
        total_penalty = 0.0

        G = thermal_solver.G
        C = thermal_solver.C
        G_ext_air = thermal_solver.G_ext_air
        G_ext_ground = thermal_solver.G_ext_ground
        T_ground = thermal_solver.T_ground

        for k in range(horizon_steps):
            # Testowana moc (wymuszamy 0 w pokojach z wyłączonym grzaniem)
            Q_hvac = Q_hvac_matrix[k] * self.enabled_mask

            # --- Szybka wektorowa symulacja fizyki na 1 krok ---
            Q_inter = np.dot(G, T_sim) - (np.sum(G, axis=1) * T_sim)
            Q_air = G_ext_air * (t_out_for[k] - T_sim)
            Q_ground = G_ext_ground * (T_ground - T_sim)

            total_Q = Q_inter + Q_air + Q_ground + q_env_for[k] + Q_hvac
            T_sim += (total_Q / C) * dt

            # --- OBLICZANIE KARY (PENALTIES) ---
            # 1. Kara za wypadnięcie z tunelu (Kwadratowa, żeby mocno karać duże odchylenia)
            below_min = np.maximum(0, T_min_hor[k] - T_sim)
            above_max = np.maximum(0, T_sim - T_max_hor[k])

            total_penalty += np.sum(below_min ** 2) * 1000.0  # Waga 1000 za bycie za zimno
            total_penalty += np.sum(above_max ** 2) * 1000.0  # Waga 1000 za bycie za gorąco

            # 2. Kara za zużycie energii (Minimalizujemy koszty)
            # Dzielimy przez max_powers, żeby znormalizować wartości 0-1 i dajemy malutką wagę.
            # Dzięki temu algorytm stara się zjechać z mocą do zera, o ile nie wypada z tunelu.
            total_penalty += np.sum((Q_hvac / self.max_powers) ** 2) * 0.1

        return total_penalty

    def step(self, current_time, dt, thermal_solver, weather_service, weather_solver):
        horizon_steps = int((self.horizon_hours * 3600) / dt)

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

        T_min_hor, T_max_hor = self._get_target_trajectories(current_time, dt, horizon_steps)

        block_size = 6
        control_steps = horizon_steps // block_size

        bounds = [(0.0, self.max_powers[i]) for k in range(control_steps) for i in range(self.num_nodes)]
        initial_guess = np.zeros(control_steps * self.num_nodes)

        res = minimize(
            self._cost_function,
            initial_guess,
            args=(thermal_solver.T, T_min_hor, T_max_hor, t_out_forecast, q_env_forecast, thermal_solver, dt,
                  horizon_steps, block_size),
            method='L-BFGS-B',
            bounds=bounds,
            options={
                'maxiter': 2,
                'ftol': 1.0,
                'eps': 1.0,
                'disp': False
            }
        )

        optimal_plan_blocked = res.x.reshape((control_steps, self.num_nodes))
        current_optimal_q = optimal_plan_blocked[0, :] * self.enabled_mask

        return current_optimal_q
