import numpy as np
import pandas as pd
import pvlib
from scipy.signal import lfilter


def generate_drift_noise(n, rho, target_std):
    step_scale = target_std * np.sqrt(1 - rho**2)

    shocks = np.random.normal(loc=0.0, scale=step_scale, size=n)

    noise = lfilter([1.0], [1.0, -rho], shocks)

    return noise


class WeatherService:
    def __init__(self, weather_path, latitude, longitude, is_digital_twin=False):
        df = pd.read_csv(weather_path)

        df["timestamp"] = pd.to_datetime(df["timestamp"])

        self.latitude = latitude
        self.longitude = longitude

        rad = np.radians(df["wind_direction"])
        df["wind_u"] = np.sin(rad)
        df["wind_v"] = np.cos(rad)

        self.weather_history = df.set_index("timestamp").sort_index()

        if is_digital_twin:
            self.randomize_weather()

    def randomize_weather(self):
        df = self.weather_history.copy()
        n = len(df)

        rho_value = 0.95

        if "temperature" in df.columns:
            df["temperature"] += generate_drift_noise(n, rho_value, 0.5)

        if "wind_u" in df.columns:
            df["wind_u"] += generate_drift_noise(n, rho_value, 0.5)

        if "wind_v" in df.columns:
            df["wind_v"] += generate_drift_noise(n, rho_value, 0.5)

        if "wind_speed" in df.columns:
            df["wind_speed"] += generate_drift_noise(n, rho_value, 0.5)
            df["wind_speed"] = df["wind_speed"].clip(lower=0.0)

        if "sun_radiation" in df.columns:
            drift_noise = generate_drift_noise(n, rho_value, 25.0)

            mask = df["sun_radiation"] > 0
            df.loc[mask, "sun_radiation"] += drift_noise[mask]
            df["sun_radiation"] = df["sun_radiation"].clip(lower=0.0)

        self.weather_history = df

    def get_weather(self, current_time):
        idx_after = self.weather_history.index.searchsorted(current_time)

        t1, t2 = self.weather_history.index[idx_after - 1], self.weather_history.index[idx_after]
        weight2 = (current_time - t1).total_seconds() / (t2 - t1).total_seconds()
        weight1 = 1 - weight2

        interp_row = (self.weather_history.loc[t1] * weight1) + (self.weather_history.loc[t2] * weight2)
        raw_weather = interp_row.to_dict()

        wind_dir_rad = np.arctan2(raw_weather["wind_u"], raw_weather["wind_v"])
        raw_weather["wind_direction"] = (np.degrees(wind_dir_rad) + 360) % 360

        solar_pos = pvlib.solarposition.get_solarposition(
            time=pd.DatetimeIndex([current_time]),
            latitude=self.latitude,
            longitude=self.longitude
        )

        raw_weather["sun_altitude"] = solar_pos["apparent_elevation"].iloc[0]
        raw_weather["sun_azimuth"] = solar_pos["azimuth"].iloc[0]

        return raw_weather
