from datetime import timedelta

import numpy as np
import pandas as pd

from shared.Co2Solver import Co2Solver
from shared.DistrictModelParser import DistrictModelParser
from shared.EnergyService import EnergyService
from shared.GasBoiler import GasBoiler
from shared.MPC import MPC
from shared.HeatPump import HeatPump
from shared.MeteringService import MeteringService
from shared.PVFarm import PVFarm
from shared.ThermalSolver import ThermalSolver
from shared.WeatherService import WeatherService
from shared.WeatherSolver import WeatherSolver


class DistrictSimulation:
    def __init__(self, weather_path, prices_path, is_digital_twin=False):
        parser = DistrictModelParser()
        parser.parse()

        self.num_nodes = parser.N
        self.metadata = parser.metadata

        self.index_to_id = {v: k for k, v in parser.nodes.items()}

        self.thermal_solver = ThermalSolver(parser.G_temp, parser.C, parser.G_ext_air, parser.G_ext_ground,
                                            self.metadata["ground_temperature"], parser.A)

        self.co2_solver = Co2Solver(parser.G_air, parser.V, parser.G_ext_air_mix, self.num_nodes, self.index_to_id)

        self.weather_solver = WeatherSolver(parser.external_connections, parser.standards, self.num_nodes)

        self.current_time = pd.Timestamp("2024-12-31 23:00+00:00")
        self.end_timestamp = pd.Timestamp("2025-12-31 23:00+00:00")

        self.weather_service = WeatherService(weather_path, self.metadata["latitude"], self.metadata["longitude"],
                                              is_digital_twin)
        self.energy_service = EnergyService(prices_path)

        self.pv_farm = PVFarm()
        self.heat_pump = HeatPump(parser.max_heat_pump_powers, parser.min_heat_pump_powers)
        self.gas_boiler = GasBoiler()

        self.mpc = MPC(self.pv_farm, self.heat_pump, self.gas_boiler, self.num_nodes, parser.max_heat_pump_powers, self.index_to_id, is_digital_twin)

        self.metering_service = MeteringService(parser.A, self.num_nodes, self.index_to_id)

    def run_step(self, dt, noise_sigma=0.0):
        weather = self.weather_service.get_weather(self.current_time)
        energy_costs = self.energy_service.get_effective_costs(self.current_time, self.pv_farm, self.heat_pump, weather,
                                                               noise_sigma)

        q_env = self.weather_solver.calculate_environmental_gains(
            weather["sun_radiation"], weather["sun_altitude"], weather["sun_azimuth"],
            weather["wind_speed"], weather["wind_direction"], weather["temperature"],
            self.thermal_solver.T
        )

        q_hvac, v_hvac = self.mpc.step(self.current_time, dt, self.thermal_solver, self.co2_solver,
                                       self.weather_service, self.weather_solver, self.energy_service, noise_sigma)

        q_total = q_env + q_hvac

        temperatures_array = self.thermal_solver.step(dt, weather["temperature"], q_total, v_hvac, noise_sigma)

        co2_array = self.co2_solver.step(self.current_time, dt, weather["co2"], v_hvac, noise_sigma)

        self.metering_service.update_meters(self.current_time, dt, energy_costs, q_hvac, v_hvac)

        output_timestamp = self.current_time.isoformat()

        self.current_time += timedelta(seconds=dt)
        if self.current_time >= self.end_timestamp:
            self.current_time = pd.Timestamp("2024-12-31 23:00+00:00")

        keys_to_remove = {"wind_u", "wind_v"}
        weather_clean = {k: round(v, 2) for k, v in weather.items() if k not in keys_to_remove}

        energy_clean = {k: round(v, 2) for k, v in energy_costs.items()}

        room_temps = {self.index_to_id[i]: round(float(temperatures_array[i]), 2) for i in range(self.num_nodes)}
        room_co2 = {self.index_to_id[i]: int(co2_array[i]) for i in range(self.num_nodes)}
        room_hvac_q = {self.index_to_id[i]: round(float(q_hvac[i]), 2) for i in range(self.num_nodes)}

        denominators = np.where(q_hvac >= 0, self.mpc.max_heat_pump_powers, self.mpc.min_heat_pump_powers)
        q_percentage = (q_hvac / denominators) * 100.0
        room_heatings = {self.index_to_id[i]: round(float(q_percentage[i]), 2) for i in range(self.num_nodes)}

        room_hvac_v = {self.index_to_id[i]: round(float(v_hvac[i] * 3600.0), 2) for i in range(self.num_nodes)}

        return {
            "timestamp": output_timestamp,
            "weather": weather_clean,
            "energy_costs": energy_clean,
            "room_temperatures": room_temps,
            "room_co2": room_co2,
            "room_hvac_q": room_hvac_q,
            "room_heatings": room_heatings,
            "room_hvac_v": room_hvac_v,
            "metering": self.metering_service.get_meter_readings()
        }
