using System.ComponentModel.DataAnnotations.Schema;

namespace shared;

[Table("telemetry")]
public class TelemetryEntity
{
    public int Id { get; }
    public long RunId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public double Temperature { get; set; }
    public double WindSpeed { get; set; }
    public double WindDirection { get; set; }
    public double SunRadiation { get; set; }
    public double SunAltitude { get; set; }
    public double SunAzimuth { get; set; }
    public int Co2 { get; set; }
    public double ElectricityCost { get; set; }
    public double GasCost { get; set; }
    public double PvFarmYield { get; set; }
    public double CopHeating { get; set; }
    public double CopCooling { get; set; }
    [Column(TypeName = "jsonb")] public string RoomTemperatures { get; set; } = "{}";
    [Column(TypeName = "jsonb")] public string RoomCo2 { get; set; } = "{}";
    [Column(TypeName = "jsonb")] public string RoomHvacQ { get; set; } = "{}";
    [Column(TypeName = "jsonb")] public string RoomHeatings { get; set; } = "{}";
    [Column(TypeName = "jsonb")] public string RoomHvacV { get; set; } = "{}";
    [Column(TypeName = "jsonb")] public string Metering { get; set; } = "{}";
}

public class SimulationTelemetryEntity : TelemetryEntity;

public class DigitalTwinTelemetryEntity : TelemetryEntity;

// dotnet ef database drop -f --project shared/shared.csproj --startup-project telemetry_service/telemetry_service.csproj
// dotnet ef migrations add m1 --project shared/shared.csproj --startup-project telemetry_service/telemetry_service.csproj
// dotnet ef database update m1 --project shared/shared.csproj --startup-project telemetry_service/telemetry_service.csproj