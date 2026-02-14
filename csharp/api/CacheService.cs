using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using shared;

namespace api;

public class CacheService(IServiceScopeFactory scopeFactory)
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    private long _currentSimulationRunId = -1;
    private ConcurrentQueue<Telemetry> _digitalTwinTelemetry = new();
    private DateTime _lastTimestamp = DateTime.MinValue;

    private ConcurrentQueue<Telemetry> _simulationTelemetry = new();

    public async Task ProcessSimulationTelemetryMessageAsync(Telemetry msg)
    {
        msg.Timestamp = msg.Timestamp.Kind switch
        {
            DateTimeKind.Local => msg.Timestamp.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(msg.Timestamp, DateTimeKind.Utc),
            _ => msg.Timestamp
        };

        var cutoffTime = msg.Timestamp.AddHours(-24);

        if (msg.RunId != _currentSimulationRunId)
        {
            await _lock.WaitAsync();
            try
            {
                if (msg.RunId != _currentSimulationRunId)
                {
                    _simulationTelemetry = new ConcurrentQueue<Telemetry>();

                    await LoadSimulationTelemetryFromDb(msg.RunId, cutoffTime);

                    _currentSimulationRunId = msg.RunId;

                    var lastFromDb = _simulationTelemetry.LastOrDefault();
                    _lastTimestamp = lastFromDb?.Timestamp ?? DateTime.MinValue;
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        if (msg.Timestamp <= _lastTimestamp) return;

        _simulationTelemetry.Enqueue(msg);

        while (_simulationTelemetry.TryPeek(out var oldestItem) && oldestItem.Timestamp < cutoffTime)
            _simulationTelemetry.TryDequeue(out _);
    }

    public async Task ProcessDigitalTwinTelemetryMessageAsync(List<Telemetry> msgs)
    {
        if (msgs.Count == 0) return;

        foreach (var msg in msgs)
            msg.Timestamp = msg.Timestamp.Kind switch
            {
                DateTimeKind.Local => msg.Timestamp.ToUniversalTime(),
                DateTimeKind.Unspecified => DateTime.SpecifyKind(msg.Timestamp, DateTimeKind.Utc),
                _ => msg.Timestamp
            };

        var firstNewMsg = msgs.First();
        var runId = firstNewMsg.RunId;
        var newBatchStartTime = firstNewMsg.Timestamp;
        var cutoffTime = newBatchStartTime.Date;

        var pastTwinMsgs = await LoadDigitalTwinTelemetryFromDb(runId, cutoffTime, newBatchStartTime);

        await _lock.WaitAsync();
        try
        {
            var newQueue = new ConcurrentQueue<Telemetry>();

            foreach (var pastMsg in pastTwinMsgs) newQueue.Enqueue(pastMsg);

            foreach (var newMsg in msgs) newQueue.Enqueue(newMsg);

            _digitalTwinTelemetry = newQueue;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task LoadSimulationTelemetryFromDb(long runId, DateTime cutoff)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

        var dbData = await db.SimulationTelemetry
            .Where(t => t.RunId == runId && t.Timestamp >= cutoff)
            .OrderByDescending(t => t.Timestamp)
            .ToListAsync();

        if (dbData.Count > 0)
        {
            var mappedData = dbData
                .OrderBy(t => t.Timestamp)
                .Select(e => new Telemetry
                {
                    RunId = e.RunId,
                    Timestamp = DateTime.SpecifyKind(e.Timestamp, DateTimeKind.Utc),
                    Weather = new WeatherData
                    {
                        Temperature = e.Temperature,
                        WindSpeed = e.WindSpeed,
                        WindDirection = e.WindDirection,
                        SunAltitude = e.SunAltitude,
                        SunAzimuth = e.SunAzimuth,
                        SunRadiation = e.SunRadiation
                    },
                    RoomTemperatures = JsonSerializer.Deserialize<Dictionary<string, double>>(e.RoomTemperatures) ??
                                       new Dictionary<string, double>()
                });

            foreach (var item in mappedData) _simulationTelemetry.Enqueue(item);
        }
    }

    private async Task<List<Telemetry>> LoadDigitalTwinTelemetryFromDb(long runId, DateTime cutoffTime,
        DateTime newBatchStartTime)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

        var pastTwinEntities = await db.DigitalTwinTelemetry
            .AsNoTracking()
            .Where(t => t.RunId == runId && t.Timestamp >= cutoffTime && t.Timestamp < newBatchStartTime)
            .OrderBy(t => t.Timestamp)
            .ToListAsync();

        var pastTwinMsgs = pastTwinEntities.Select(e => new Telemetry
        {
            RunId = e.RunId,
            Timestamp = e.Timestamp,
            Weather = new WeatherData
            {
                Temperature = e.Temperature,
                WindSpeed = e.WindSpeed,
                WindDirection = e.WindDirection,
                SunRadiation = e.SunRadiation,
                SunAltitude = e.SunAltitude,
                SunAzimuth = e.SunAzimuth
            },
            RoomTemperatures = JsonSerializer.Deserialize<Dictionary<string, double>>(e.RoomTemperatures) ??
                               new Dictionary<string, double>()
        }).ToList();

        return pastTwinMsgs;
    }

    public List<Telemetry> GetSimulationTelemetry()
    {
        return _simulationTelemetry.ToList();
    }

    public List<Telemetry> GetDigitalTwinTelemetry()
    {
        return _digitalTwinTelemetry.ToList();
    }
}