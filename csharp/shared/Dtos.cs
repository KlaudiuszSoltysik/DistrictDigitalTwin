using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace shared;

public class Telemetry
{
    [JsonPropertyName("run_id")] public long RunId { get; set; }
    [JsonPropertyName("timestamp")] public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("weather")] public required WeatherData Weather { get; set; }

    [JsonPropertyName("energy_costs")] public required EnergyCostsData EnergyCosts { get; set; }

    [JsonPropertyName("room_temperatures")]
    public Dictionary<string, double> RoomTemperatures { get; set; } = new();

    [JsonPropertyName("room_co2")] public Dictionary<string, int> RoomCo2 { get; set; } = new();

    [JsonPropertyName("room_hvac_q")] public Dictionary<string, double> RoomHvacQ { get; set; } = new();

    [JsonPropertyName("room_heatings")] public Dictionary<string, double> RoomHeatings { get; set; } = new();

    [JsonPropertyName("room_hvac_v")] public Dictionary<string, double> RoomHvacV { get; set; } = new();

    [JsonPropertyName("metering")] public required MeteringData Metering { get; set; }
}

public class WeatherData
{
    [JsonPropertyName("temperature")] public double Temperature { get; set; }
    [JsonPropertyName("wind_speed")] public double WindSpeed { get; set; }
    [JsonPropertyName("wind_direction")] public double WindDirection { get; set; }
    [JsonPropertyName("sun_radiation")] public double SunRadiation { get; set; }
    [JsonPropertyName("sun_altitude")] public double SunAltitude { get; set; }
    [JsonPropertyName("sun_azimuth")] public double SunAzimuth { get; set; }
    [JsonPropertyName("co2")] public int Co2 { get; set; }
}

public class EnergyCostsData
{
    [JsonPropertyName("pv_farm_yield")] public double PvFarmYield { get; set; }
    [JsonPropertyName("cop_heating")] public double CopHeating { get; set; }
    [JsonPropertyName("cop_cooling")] public double CopCooling { get; set; }
}

public class MeteringData
{
    [JsonPropertyName("admin_meters")] public Dictionary<string, double> AdminMeters { get; set; } = new();

    [JsonPropertyName("tenant_meters")]
    public Dictionary<string, Dictionary<string, double>> TenantMeters { get; set; } = new();
}

public class Config
{
    [JsonPropertyName("is_paused")] public bool? IsPaused { get; set; }
    [JsonPropertyName("simulation_speed")] public int? SimulationSpeed { get; set; }
    [JsonPropertyName("simulation_step")] public int? SimulationStep { get; set; }

    [JsonPropertyName("room_noise_sigma")] public double? RoomNoiseSigma { get; set; }
}

public class SimulationConfig
{
    [JsonPropertyName("config")] public required Config Config { get; set; }
}

public class ControlMessage
{
    [JsonPropertyName("action")] public required string Action { get; set; }
    [JsonPropertyName("target_name")] public string? TargetName { get; set; }
    [JsonPropertyName("target_config")] public Config? TargetConfig { get; set; }
}

public class DigitalTwinRequest
{
    [JsonPropertyName("start_timestamp")] public DateTimeOffset StartTimestamp { get; set; }
    [JsonPropertyName("t")] public Dictionary<string, double> T { get; set; } = new();
    [JsonPropertyName("co2")] public Dictionary<string, int> Co2 { get; set; } = new();
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
    public int Co2 { get; set; }
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
    public List<int> Co2 { get; set; } = [];
}