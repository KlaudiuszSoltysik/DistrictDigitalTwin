using System.ComponentModel.DataAnnotations.Schema;

namespace shared;

[Table("telemetry")]
public class TelemetryEntity
{
    public int Id { get; }
    public long RunId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public double Temperature { get; init; }
    public double WindSpeed { get; init; }
    public double WindDirection { get; init; }
    public double SunRadiation { get; init; }
    public double SunAltitude { get; init; }
    public double SunAzimuth { get; init; }
    public int Co2 { get; init; }
    public double ElectricityCost { get; init; }
    public double GasCost { get; init; }
    public double PvFarmYield { get; init; }
    public double CopHeating { get; init; }
    public double CopCooling { get; init; }
    [Column(TypeName = "jsonb")] public string RoomTemperatures { get; init; } = "{}";
    [Column(TypeName = "jsonb")] public string RoomCo2 { get; init; } = "{}";
    [Column(TypeName = "jsonb")] public string RoomHvacQ { get; init; } = "{}";
    [Column(TypeName = "jsonb")] public string RoomHeatings { get; init; } = "{}";
    [Column(TypeName = "jsonb")] public string RoomHvacV { get; init; } = "{}";
    [Column(TypeName = "jsonb")] public string Metering { get; init; } = "{}";
}

public class SimulationTelemetryEntity : TelemetryEntity;

public class DigitalTwinTelemetryEntity : TelemetryEntity;

// dotnet ef database drop -f --project shared/shared.csproj --startup-project telemetry_service/telemetry_service.csproj
// dotnet ef migrations add m1 --project shared/shared.csproj --startup-project telemetry_service/telemetry_service.csproj
// dotnet ef database update m1 --project shared/shared.csproj --startup-project telemetry_service/telemetry_service.csproj