using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

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
}

public class SimulationConfig
{
    [JsonPropertyName("config")] public Config Config { get; set; }
}

public class ControlMessage
{
    [JsonPropertyName("action")] public string Action { get; set; }
    [JsonPropertyName("target_name")] public string? TargetName { get; set; }
    [JsonPropertyName("target_config")] public Config? TargetConfig { get; set; }
}

public class DigitalTwinRequest
{
    [JsonPropertyName("start_timestamp")] public DateTimeOffset StartTimestamp { get; set; }
    [JsonPropertyName("t")] public Dictionary<string, double> T { get; set; } = new();
    [JsonPropertyName("hvac_q")] public Dictionary<string, double> HvacQ { get; set; } = new();
}

[BsonIgnoreExtraElements]
public class ApartmentConfig
{
    public required string BuildingId { get; set; }
    public required string ApartmentId { get; set; }
    public List<RoomConfig> Rooms { get; set; } = [];
}

public class RoomConfig
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required HvacControl HvacControl { get; set; }
}

public class HvacControl
{
    public List<double> Temperatures { get; set; } = [];
    public double Tolerance { get; set; } = 0.1;
    public List<bool> IsEnabled { get; set; } = [];
}

public class AllApartmentsConfig
{
    public required string BuildingId { get; set; }
    public required string ApartmentId { get; set; }
    public List<AllApartmentsConfigRoom> Rooms { get; set; } = [];
}

public class AllApartmentsConfigRoom
{
    public required string Id { get; set; }
    public required AllApartmentsConfigHvac Hvac { get; set; }
}

public class AllApartmentsConfigHvac
{
    public List<double> TemperaturesMin { get; set; } = [];
    public List<double?> Temperatures { get; set; } = [];
    public List<double> TemperaturesMax { get; set; } = [];
    public List<bool> IsEnabled { get; set; } = [];
}