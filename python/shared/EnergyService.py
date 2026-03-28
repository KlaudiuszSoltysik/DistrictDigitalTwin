import pandas as pd


class EnergyService:
    def __init__(self, prices_path):
        df = pd.read_csv(prices_path)
        df["timestamp"] = pd.to_datetime(df["timestamp"])
        self.prices_history = df.set_index("timestamp").sort_index()

    def get_effective_costs(self, current_time, pv_farm, heat_pump, weather):
        idx_after = self.prices_history.index.searchsorted(current_time)

        cop_heating, cop_cooling = heat_pump.get_cop(weather)
        pv_yield_kw = pv_farm.get_power_prognosis(weather)

        elec_price_kwh = float(self.prices_history.iloc[idx_after]["electricity_price"]) / 1000.0
        gas_price_kwh = float(self.prices_history.iloc[idx_after]["gas_price"]) / 1000.0

        return {
            "electricity_cost": elec_price_kwh,
            "gas_cost": gas_price_kwh,
            "pv_farm_yield": pv_yield_kw,
            "cop_heating": cop_heating,
            "cop_cooling": cop_cooling
        }
