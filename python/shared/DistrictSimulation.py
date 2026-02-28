from datetime import timedelta

import pandas as pd

from shared.DistrictModelParser import DistrictModelParser
from shared.HVAC import HVAC
from shared.ThermalSolver import ThermalSolver
from shared.WeatherService import WeatherService
from shared.WeatherSolver import WeatherSolver


class DistrictSimulation:
    def __init__(self, config_path, weather_path, is_digital_twin=False):
        parser = DistrictModelParser(config_path)
        self.metadata = parser.raw_data["metadata"]
        G, G_ext_air, G_ext_ground, C, N, external_connections, standards, nodes = parser.parse()

        self.thermal_solver = ThermalSolver(G=G, C=C, G_ext_air=G_ext_air, G_ext_ground=G_ext_ground,
            T_ground=self.metadata["ground_temperature"])

        self.weather_solver = WeatherSolver(external_connections, standards, N)

        self.current_time = pd.Timestamp("2024-12-31 23:00+00:00")
        self.end_timestamp = pd.Timestamp("2025-12-31 23:00+00:00")

        self.weather_service = WeatherService(weather_path, self.metadata["latitude"], self.metadata["longitude"],
                                              is_digital_twin)

        # TODO: change that
        self.hvac = HVAC(num_nodes=len(C), base_setpoint=21.0, time_constant_hours=2.0)

        self.index_to_id = {v: k for k, v in nodes.items()}

    def run_step(self, dt_seconds, drift_sigma=0.0):
        weather = self.weather_service.get_weather(self.current_time)

        q_env = self.weather_solver.calculate_environmental_gains(weather["sun_radiation"], weather["sun_altitude"],
            weather["sun_azimuth"], weather["wind_speed"], weather["wind_direction"], weather["temperature"],
            self.thermal_solver.T)

        q_hvac = self.hvac.step(dt_seconds, self.thermal_solver.T)
        q_total = q_env + q_hvac

        temperatures_array = self.thermal_solver.step(dt_seconds, weather["temperature"], q_total, drift_sigma)

        temperatures_array = [round(x, 2) for x in temperatures_array]

        keys_to_remove = {"wind_u", "wind_v"}
        weather_clean = {k: round(v, 2) for k, v in weather.items() if k not in keys_to_remove}

        room_temps = {self.index_to_id[i]: float(temperatures_array[i]) for i in range(len(temperatures_array))}

        output_timestamp = self.current_time.isoformat()
        self.current_time += timedelta(seconds=dt_seconds)

        if self.current_time >= self.end_timestamp:
            self.current_time = pd.Timestamp("2024-12-31 23:00+00:00")

        return {"timestamp": output_timestamp, "weather": weather_clean, "room_temperatures": room_temps}