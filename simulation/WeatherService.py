import numpy as np
import pandas as pd
import pvlib


class WeatherService:
    def __init__(self, weather_path, timezone_str, latitude, longitude):
        df = pd.read_csv(weather_path)
        self.latitude = latitude
        self.longitude = longitude

        df["timestamp"] = pd.to_datetime(df["timestamp"], utc=True).dt.tz_convert(timezone_str)

        rad = np.radians(df["wind_direction"])
        df["wind_u"] = np.sin(rad)
        df["wind_v"] = np.cos(rad)

        self.weather_history = df.set_index("timestamp").sort_index()

    def get_weather(self, current_time):
        now = pd.Timestamp(current_time)
        now = now.tz_convert(self.weather_history.index.tz)

        idx_after = self.weather_history.index.searchsorted(now)

        t1, t2 = self.weather_history.index[idx_after - 1], self.weather_history.index[idx_after]
        weight2 = (now - t1).total_seconds() / (t2 - t1).total_seconds()
        weight1 = 1 - weight2

        interp_row = (self.weather_history.loc[t1] * weight1) + (self.weather_history.loc[t2] * weight2)
        raw_weather = interp_row.to_dict()

        wind_dir_rad = np.arctan2(raw_weather["wind_u"], raw_weather["wind_v"])
        raw_weather["wind_direction"] = (np.degrees(wind_dir_rad) + 360) % 360

        solar_pos = pvlib.solarposition.get_solarposition(
            time=pd.DatetimeIndex([now]),
            latitude=self.latitude,
            longitude=self.longitude
        )

        raw_weather["sun_altitude"] = solar_pos["apparent_elevation"].iloc[0]
        raw_weather["sun_azimuth"] = solar_pos["azimuth"].iloc[0]

        return raw_weather
