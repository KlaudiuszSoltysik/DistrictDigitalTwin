using System.ComponentModel.DataAnnotations.Schema;

namespace shared;

[Table("telemetry")]
public class TelemetryEntity
{
    public int Id { get; set; }
    public long RunId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public double Temperature { get; set; }
    public double WindSpeed { get; set; }
    public double WindDirection { get; set; }
    public double SunRadiation { get; set; }
    public double SunAltitude { get; set; }
    public double SunAzimuth { get; set; }
    [Column(TypeName = "jsonb")] public string RoomTemperatures { get; set; } = "{}";
    [Column(TypeName = "jsonb")] public string RoomHvacQ { get; set; } = "{}";
    [Column(TypeName = "jsonb")] public string RoomHeatings { get; set; } = "{}";
}

public class SimulationTelemetryEntity : TelemetryEntity;

public class DigitalTwinTelemetryEntity : TelemetryEntity;