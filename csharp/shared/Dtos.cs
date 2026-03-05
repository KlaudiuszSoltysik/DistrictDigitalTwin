using System.Text.Json.Serialization;

namespace shared;

public class Telemetry
{
    [JsonPropertyName("run_id")] public long RunId { get; set; }
    [JsonPropertyName("timestamp")] public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("weather")] public WeatherData Weather { get; set; }

    [JsonPropertyName("room_temperatures")]
    public Dictionary<string, double> RoomTemperatures { get; set; } = new();

    [JsonPropertyName("room_hvac_q")] public Dictionary<string, double> RoomHvacQ { get; set; } = new();

    [JsonPropertyName("room_heatings")] public Dictionary<string, double> RoomHeatings { get; set; } = new();
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

public class Config
{
    [JsonPropertyName("is_paused")] public bool? IsPaused { get; set; }
    [JsonPropertyName("simulation_speed")] public int? SimulationSpeed { get; set; }
    [JsonPropertyName("simulation_step")] public int? SimulationStep { get; set; }

    [JsonPropertyName("room_temperature_noise_sigma")]
    public double? RoomTemperatureNoiseSigma { get; set; }

    [JsonPropertyName("p_band")] public double? P_Band { get; set; }
    [JsonPropertyName("t_i")] public double? T_I { get; set; }
}

public class SimulationConfig
{
    [JsonPropertyName("config")] public Config Config { get; set; }
}

public class ControlMessage
{
    [JsonPropertyName("action")] public string Action { get; set; }
    [JsonPropertyName("device_name")] public string? DeviceName { get; set; }
    [JsonPropertyName("target_config")] public Config? TargetConfig { get; set; }
}

public class DigitalTwinRequest
{
    [JsonPropertyName("start_timestamp")] public DateTimeOffset StartTimestamp { get; set; }
    [JsonPropertyName("t")] public Dictionary<string, double> T { get; set; } = new();
    [JsonPropertyName("hvac_q")] public Dictionary<string, double> HvacQ { get; set; } = new();
}

public class HvacControl
{
    public required string ApartmentId { get; set; }
    public List<HvacRoomControl> HvacRoomControls { get; set; } = [];
}

public class HvacRoomControl
{
    public required string RoomId { get; set; }
    public List<double> Temperatures { get; set; } = [];
}

public class RoomInformation
{
    public required string Id { get; set; }
    public required string Name { get; set; }
}