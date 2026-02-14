using System.Text.Json.Serialization;

namespace shared;

public class Telemetry
{
    [JsonPropertyName("run_id")] public long RunId { get; set; }
    [JsonPropertyName("timestamp")] public DateTime Timestamp { get; set; }

    [JsonPropertyName("room_temperatures")]
    public Dictionary<string, double> RoomTemperatures { get; set; } = new();

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

public class SimulationConfig
{
    [JsonPropertyName("is_paused")] public bool IsPaused { get; set; }
    [JsonPropertyName("simulation_speed")] public int SimulationSpeed { get; set; }
    [JsonPropertyName("simulation_step")] public int SimulationStep { get; set; }

    [JsonPropertyName("room_temperature_noise_sigma")]
    public double RoomTemperatureNoiseSigma { get; set; }
}

public class SimulationStatus
{
    [JsonPropertyName("config")] public SimulationConfig Config { get; set; }
}

public class ControlMessage
{
    [JsonPropertyName("action")] public string Action { get; set; }
    [JsonPropertyName("target_config")] public SimulationConfig? TargetConfig { get; set; }
}