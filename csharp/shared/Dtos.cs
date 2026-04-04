using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace shared;

public class Telemetry
{
    [JsonPropertyName("run_id")] public long RunId { get; init; }
    [JsonPropertyName("timestamp")] public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("weather")] public required WeatherData Weather { get; init; }

    [JsonPropertyName("energy_costs")] public required EnergyCostsData EnergyCosts { get; init; }

    [JsonPropertyName("room_temperatures")]
    public Dictionary<string, double> RoomTemperatures { get; init; } = new();

    [JsonPropertyName("room_co2")] public Dictionary<string, int> RoomCo2 { get; init; } = new();

    [JsonPropertyName("room_hvac_q")] public Dictionary<string, double> RoomHvacQ { get; init; } = new();

    [JsonPropertyName("room_heatings")] public Dictionary<string, double> RoomHeatings { get; init; } = new();

    [JsonPropertyName("room_hvac_v")] public Dictionary<string, double> RoomHvacV { get; init; } = new();

    [JsonPropertyName("metering")] public required MeteringData Metering { get; init; }
}

public class WeatherData
{
    [JsonPropertyName("temperature")] public double Temperature { get; init; }
    [JsonPropertyName("wind_speed")] public double WindSpeed { get; init; }
    [JsonPropertyName("wind_direction")] public double WindDirection { get; init; }
    [JsonPropertyName("sun_radiation")] public double SunRadiation { get; init; }
    [JsonPropertyName("sun_altitude")] public double SunAltitude { get; init; }
    [JsonPropertyName("sun_azimuth")] public double SunAzimuth { get; init; }
    [JsonPropertyName("co2")] public int Co2 { get; init; }
}

public class EnergyCostsData
{
    [JsonPropertyName("electricity_cost")] public double ElectricityCost { get; init; }
    [JsonPropertyName("gas_cost")] public double GasCost { get; init; }
    [JsonPropertyName("pv_farm_yield")] public double PvFarmYield { get; init; }
    [JsonPropertyName("cop_heating")] public double CopHeating { get; init; }
    [JsonPropertyName("cop_cooling")] public double CopCooling { get; init; }
}

public class MeteringData
{
    [JsonPropertyName("admin_meters")] public Dictionary<string, double> AdminMeters { get; init; } = new();

    [JsonPropertyName("tenant_meters")]
    public Dictionary<string, Dictionary<string, double>> TenantMeters { get; init; } = new();
}

public class Config
{
    [JsonPropertyName("is_paused")] public bool? IsPaused { get; init; }
    [JsonPropertyName("simulation_speed")] public int? SimulationSpeed { get; init; }
    [JsonPropertyName("simulation_step")] public int? SimulationStep { get; init; }

    [JsonPropertyName("room_noise_sigma")] public double? RoomNoiseSigma { get; init; }
}

public abstract class SimulationConfig
{
    [JsonPropertyName("config")] public required Config Config { get; init; }
}

public class ControlMessage
{
    [JsonPropertyName("action")] public required string Action { get; init; }
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
    public required string BuildingId { get; init; }
    public required string ApartmentId { get; init; }
    public List<RoomConfig> Rooms { get; init; } = [];
}

public class RoomConfig
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required HvacControl HvacControl { get; init; }
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
    public required string BuildingId { get; init; }
    public required string ApartmentId { get; init; }
    public List<AllApartmentsConfigRoom> Rooms { get; init; } = [];
}

public class AllApartmentsConfigRoom
{
    public required string Id { get; init; }
    public required AllApartmentsConfigHvac Hvac { get; init; }
}

public class AllApartmentsConfigHvac
{
    public List<double> TemperaturesMin { get; } = [];
    public List<double?> Temperatures { get; } = [];
    public List<double> TemperaturesMax { get; } = [];
    public List<int> Co2 { get; } = [];
}