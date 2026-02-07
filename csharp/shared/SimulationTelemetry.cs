using System.Text.Json.Serialization;

namespace shared;

public class SimulationTelemetry
{
    [JsonPropertyName("run_id")] public long RunId { get; set; }
    [JsonPropertyName("timestamp")] public DateTime Timestamp { get; set; }
    [JsonPropertyName("room_temperatures")] public Dictionary<string, double> RoomTemperatures { get; set; } = new();
    [JsonPropertyName("weather")] public WeatherData Weather { get; set; }
}

public class WeatherData
{
    [JsonPropertyName("temperature")] public double Temperature { get; set; }
    [JsonPropertyName("wind_speed")] public double WindSpeed { get; set; }
    [JsonPropertyName("wind_direction")] public double WindDirection { get; set; }
    [JsonPropertyName("sun_radiation")] public double SunRadiation { get; set; }
    [JsonPropertyName("sun_altitude")] public double SunAltitude { get; set; }
    [JsonPropertyName("sun_azimuth")] public double SunAzimuth { get; set; }
}