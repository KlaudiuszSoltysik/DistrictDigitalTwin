using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using shared;

namespace telemetry_service.Consumers;

public class SimulationTelemetryConsumer(TelemetryDbContext db) : IConsumer<Telemetry>
{
    private static long _currentRunId;
    private static int _lastProcessedHour = -1;

    public async Task Consume(ConsumeContext<Telemetry> context)
    {
        var msg = context.Message;
        var currentTimestamp = msg.Timestamp;

        var isNewRun = msg.RunId != _currentRunId;
        var isNewHour = currentTimestamp.Hour != _lastProcessedHour;

        if (isNewRun)
        {
            await db.SimulationTelemetry
                .Where(t => t.RunId != msg.RunId)
                .ExecuteDeleteAsync();

            _currentRunId = msg.RunId;
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
            RoomTemperatures = JsonSerializer.Serialize(msg.RoomTemperatures)
        };

        db.SimulationTelemetry.Add(entity);
        await db.SaveChangesAsync();

        if (isNewRun || isNewHour)
        {
            _lastProcessedHour = currentTimestamp.Hour;

            var twinRequest = new DigitalTwinRequest
            {
                StartTimestamp = currentTimestamp,
                T = msg.RoomTemperatures
            };

            var sendEndpoint = await context.GetSendEndpoint(new Uri("queue:digital-twin-commands"));
            await sendEndpoint.Send(twinRequest);
        }
    }
}