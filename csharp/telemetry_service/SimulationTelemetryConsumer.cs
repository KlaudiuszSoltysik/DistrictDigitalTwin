using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using shared;

namespace telemetry_service;

public class SimulationTelemetryConsumer(TelemetryDbContext db) : IConsumer<Telemetry>
{
    private static long _currentRunId;

    public async Task Consume(ConsumeContext<Telemetry> context)
    {
        var msg = context.Message;

        if (_currentRunId != msg.RunId)
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM SimulationTelemetry WHERE \"RunId\" != {0}", msg.RunId);

            _currentRunId = msg.RunId;
        }

        var entity = new SimulationTelemetryEntity
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

        db.SimulationTelemetry.Add(entity);
        await db.SaveChangesAsync();
    }
}