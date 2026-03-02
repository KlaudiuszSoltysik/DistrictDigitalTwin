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
        parser.parse()

        self.metadata = parser.metadata

        self.thermal_solver = ThermalSolver(parser.G, parser.C, parser.G_ext_air, parser.G_ext_ground,
                                            self.metadata["ground_temperature"])

        self.weather_solver = WeatherSolver(parser.external_connections, parser.standards, parser.N)

        # TODO: change that ?
        self.current_time = pd.Timestamp("2024-12-31 23:00+00:00")
        self.end_timestamp = pd.Timestamp("2025-12-31 23:00+00:00")

        self.weather_service = WeatherService(weather_path, self.metadata["latitude"], self.metadata["longitude"],
                                              is_digital_twin)

        self.hvac = HVAC(parser.N, parser.max_heating_powers)

        self.index_to_id = {v: k for k, v in parser.nodes.items()}

    def run_step(self, dt, drift_sigma=0.0):
        weather = self.weather_service.get_weather(self.current_time)

        q_env = self.weather_solver.calculate_environmental_gains(
            weather["sun_radiation"],
            weather["sun_altitude"],
            weather["sun_azimuth"],
            weather["wind_speed"],
            weather["wind_direction"],
            weather["temperature"],
            self.thermal_solver.T
        )

        q_hvac = self.hvac.step(dt, self.thermal_solver.T)
        q_total = q_env + q_hvac

        temperatures_array = self.thermal_solver.step(dt, weather["temperature"], q_total, drift_sigma)

        temperatures_array = [round(x, 2) for x in temperatures_array]

        keys_to_remove = {"wind_u", "wind_v"}
        weather_clean = {k: round(v, 2) for k, v in weather.items() if k not in keys_to_remove}

        room_temps = {self.index_to_id[i]: float(temperatures_array[i]) for i in range(len(temperatures_array))}

        q_percentage = (q_hvac / self.hvac.max_powers) * 100.0
        room_heatings = {self.index_to_id[i]: round(float(q_percentage[i]), 2) for i in range(len(q_percentage))}

        output_timestamp = self.current_time.isoformat()
        self.current_time += timedelta(seconds=dt)

        if self.current_time >= self.end_timestamp:
            self.current_time = pd.Timestamp("2024-12-31 23:00+00:00")

        return {
            "timestamp": output_timestamp,
            "weather": weather_clean,
            "room_temperatures": room_temps,
            "room_heatings": room_heatings
        }
