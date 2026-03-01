using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using shared;

namespace telemetry_service.Consumers;

public class DigitalTwinTelemetryConsumer(TelemetryDbContext db, ILogger<DigitalTwinTelemetryConsumer> logger) : IConsumer<Telemetry[]>
{
    private static long _currentRunId = -1;
    private static readonly SemaphoreSlim Lock = new(1, 1);

    public async Task Consume(ConsumeContext<Telemetry[]> context)
    {
        if (_currentRunId == -1)
        {
            await Lock.WaitAsync();
            try
            {
                if (_currentRunId == -1)
                {
                    await db.DigitalTwinTelemetry.ExecuteDeleteAsync();

                    _currentRunId = 1;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error during cleanup. Method: {method}", "Consume");
            }
            finally
            {
                Lock.Release();
            }
        }

        try
        {
            var msgs = context.Message.ToList();

            if (msgs.Count == 0) return;

            var firstMsg = msgs.First();

            await db.DigitalTwinTelemetry
                .Where(t => t.Timestamp >= firstMsg.Timestamp)
                .ExecuteDeleteAsync();

            var entities = msgs.Select(m => new DigitalTwinTelemetryEntity
            {
                RunId = m.RunId,
                Timestamp = m.Timestamp,
                Temperature = m.Weather.Temperature,
                WindSpeed = m.Weather.WindSpeed,
                WindDirection = m.Weather.WindDirection,
                SunRadiation = m.Weather.SunRadiation,
                SunAltitude = m.Weather.SunAltitude,
                SunAzimuth = m.Weather.SunAzimuth,
                RoomTemperatures = JsonSerializer.Serialize(m.RoomTemperatures),
                RoomHeatings = JsonSerializer.Serialize(m.RoomHeatings)
            }).ToList();

            await db.DigitalTwinTelemetry.AddRangeAsync(entities);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse and process message. Method: {method}", "Consume");
        }
    }
}