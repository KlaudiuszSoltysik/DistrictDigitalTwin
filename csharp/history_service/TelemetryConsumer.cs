using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using shared;

namespace history_service;

public class TelemetryConsumer(HistoryDbContext db) : IConsumer<SimulationTelemetry>
{
    private static long _currentRunIdCache;

    public async Task Consume(ConsumeContext<SimulationTelemetry> context)
    {
        var msg = context.Message;

        if (_currentRunIdCache != msg.RunId)
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM telemetry WHERE \"RunId\" != {0}", msg.RunId);

            _currentRunIdCache = msg.RunId;
        }

        var entity = new TelemetryEntity
        {
            RunId = msg.RunId,
            Timestamp = msg.Timestamp.ToUniversalTime(),
            Temperature = msg.Weather.Temperature,
            WindSpeed = msg.Weather.WindSpeed,
            WindDirection = msg.Weather.WindDirection,
            SunRadiation = msg.Weather.SunRadiation,
            SunAltitude = msg.Weather.SunAltitude,
            SunAzimuth = msg.Weather.SunAzimuth,
            RoomTemperatures = JsonSerializer.Serialize(msg.RoomTemperatures)
        };

        db.Telemetry.Add(entity);
        await db.SaveChangesAsync();
    }
}