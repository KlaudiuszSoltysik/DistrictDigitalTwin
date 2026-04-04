using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using shared;

namespace telemetry_service.Consumers;

public abstract class DigitalTwinTelemetryConsumer(TelemetryDbContext db, ILogger<DigitalTwinTelemetryConsumer> logger)
    : IConsumer<Telemetry[]>
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

            var entities = msgs.Select(msg => new DigitalTwinTelemetryEntity
            {
                RunId = msg.RunId,
                Timestamp = msg.Timestamp,
                Temperature = msg.Weather.Temperature,
                WindSpeed = msg.Weather.WindSpeed,
                WindDirection = msg.Weather.WindDirection,
                SunRadiation = msg.Weather.SunRadiation,
                SunAltitude = msg.Weather.SunAltitude,
                SunAzimuth = msg.Weather.SunAzimuth,
                Co2 = msg.Weather.Co2,
                ElectricityCost = msg.EnergyCosts.ElectricityCost,
                GasCost = msg.EnergyCosts.GasCost,
                PvFarmYield = msg.EnergyCosts.PvFarmYield,
                CopHeating = msg.EnergyCosts.CopHeating,
                CopCooling = msg.EnergyCosts.CopCooling,
                RoomTemperatures = JsonSerializer.Serialize(msg.RoomTemperatures),
                RoomCo2 = JsonSerializer.Serialize(msg.RoomCo2),
                RoomHvacQ = JsonSerializer.Serialize(msg.RoomHvacQ),
                RoomHeatings = JsonSerializer.Serialize(msg.RoomHeatings),
                RoomHvacV = JsonSerializer.Serialize(msg.RoomHvacV),
                Metering = JsonSerializer.Serialize(msg.Metering)
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