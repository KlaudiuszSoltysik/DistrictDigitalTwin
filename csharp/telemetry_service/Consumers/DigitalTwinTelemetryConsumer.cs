using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using shared;

namespace telemetry_service.Consumers;

public class DigitalTwinTelemetryConsumer(TelemetryDbContext db) : IConsumer<Telemetry[]>
{
    public async Task Consume(ConsumeContext<Telemetry[]> context)
    {
        var messageArray = context.Message;
        var msgList = messageArray.ToList();

        var firstMsg = msgList.First();

        await db.DigitalTwinTelemetry
            .Where(t => t.Timestamp >= firstMsg.Timestamp)
            .ExecuteDeleteAsync();

        var entities = msgList.Select(m => new DigitalTwinTelemetryEntity
        {
            RunId = m.RunId,
            Timestamp = m.Timestamp,
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