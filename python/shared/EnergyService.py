import pandas as pd


class EnergyService:
    def __init__(self, prices_path, weather_service):
        df = pd.read_csv(prices_path)
        df["timestamp"] = pd.to_datetime(df["timestamp"])
        self.prices_history = df.set_index("timestamp").sort_index()

        self.weather_service = weather_service

    def get_effective_costs(self, current_time):
        idx_after = self.prices_history.index.searchsorted(current_time)

        return {
            "electricity_cost": float(self.prices_history.iloc[idx_after]["electricity_price"]) / 1000,
            "gas_cost": float(self.prices_history.iloc[idx_after]["gas_price"]) / 1000,
            "res_yield": 0.0,
            "cop_heating": 0.0,
            "cop_cooling": 0.0
        }
