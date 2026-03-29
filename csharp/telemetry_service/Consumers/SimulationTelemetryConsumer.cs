using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using shared;

namespace telemetry_service.Consumers;

public class SimulationTelemetryConsumer(TelemetryDbContext db, ILogger<SimulationTelemetryConsumer> logger)
    : IConsumer<Telemetry>
{
    private static long _currentRunId = -1;
    private static int _lastProcessedHour = -1;

    private static readonly SemaphoreSlim Lock = new(1, 1);

    public async Task Consume(ConsumeContext<Telemetry> context)
    {
        try
        {
            var msg = context.Message;
            var currentTimestamp = msg.Timestamp;

            if (msg.RunId != _currentRunId)
            {
                await Lock.WaitAsync();
                try
                {
                    if (msg.RunId != _currentRunId)
                    {
                        await db.SimulationTelemetry
                            .Where(t => t.RunId != msg.RunId)
                            .ExecuteDeleteAsync();

                        _currentRunId = msg.RunId;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to delete old telemetry. Method: {method}", "Consume");
                }
                finally
                {
                    Lock.Release();
                }
            }

            var entity = new SimulationTelemetryEntity
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
            };

            db.SimulationTelemetry.Add(entity);
            await db.SaveChangesAsync();

            if (currentTimestamp.Hour != _lastProcessedHour)
            {
                _lastProcessedHour = currentTimestamp.Hour;

                var twinRequest = new DigitalTwinRequest
                {
                    StartTimestamp = currentTimestamp,
                    T = msg.RoomTemperatures,
                    Co2 = msg.RoomCo2
                };

                var sendEndpoint = await context.GetSendEndpoint(new Uri("queue:digital-twin-commands"));
                await sendEndpoint.Send(twinRequest);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse and process message. Method: {method}", "Consume");
        }
    }
}