using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using shared;

namespace telemetry_service.Consumers;

public class DigitalTwinTelemetryConsumer(TelemetryDbContext db) : IConsumer<Telemetry[]>
{
    private static long _currentRunId;

    public async Task Consume(ConsumeContext<Telemetry[]> context)
    {
        var messageArray = context.Message;

        var msgList = messageArray.ToList();

        if (msgList.Count == 0) return;

        var msg = msgList.FirstOrDefault();

        if (msg == null) return;

        if (_currentRunId != msg.RunId)
        {
            await db.DigitalTwinTelemetry
                .Where(t => t.RunId != msg.RunId)
                .ExecuteDeleteAsync();

            _currentRunId = msg.RunId;
        }

        var currentRunId = msg.RunId;
        var startTimestamp = msgList.Min(m => m.Timestamp).ToUniversalTime();

        await db.DigitalTwinTelemetry
            .Where(t => t.RunId != currentRunId)
            .ExecuteDeleteAsync();

        await db.DigitalTwinTelemetry
            .Where(t => t.RunId == currentRunId && t.Timestamp >= startTimestamp)
            .ExecuteDeleteAsync();

        var entities = msgList.Select(m => new DigitalTwinTelemetryEntity
        {
            RunId = m.RunId,
            Timestamp = m.Timestamp.ToUniversalTime(),
            Temperature = m.Weather.Temperature,
            WindSpeed = m.Weather.WindSpeed,
            WindDirection = m.Weather.WindDirection,
            SunRadiation = m.Weather.SunRadiation,
            SunAltitude = m.Weather.SunAltitude,
            SunAzimuth = m.Weather.SunAzimuth,
            RoomTemperatures = JsonSerializer.Serialize(m.RoomTemperatures)
        }).ToList();

        await db.DigitalTwinTelemetry.AddRangeAsync(entities);
        await db.SaveChangesAsync();
    }
}