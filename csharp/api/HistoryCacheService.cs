using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using shared;

namespace api;

public class HistoryCacheService(IServiceScopeFactory scopeFactory)
{
    private const int MaxHistorySize = 2000;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private long _currentRunId = -1;

    private ConcurrentQueue<SimulationTelemetry> _history = new();

    private DateTime _lastTimestamp = DateTime.MinValue;

    public async Task ProcessMessageAsync(SimulationTelemetry msg)
    {
        if (msg.Timestamp.Kind == DateTimeKind.Unspecified)
        {
            msg.Timestamp = DateTime.SpecifyKind(msg.Timestamp, DateTimeKind.Utc);
        }

        if (msg.RunId != _currentRunId)
        {
            await _lock.WaitAsync();
            try
            {
                if (msg.RunId != _currentRunId)
                {
                    _history = new ConcurrentQueue<SimulationTelemetry>();

                    await LoadHistoryFromDb(msg.RunId);

                    _currentRunId = msg.RunId;

                    var lastFromDb = _history.LastOrDefault();
                    _lastTimestamp = lastFromDb?.Timestamp ?? DateTime.MinValue;
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        if (msg.Timestamp <= _lastTimestamp)
        {
            return;
        }

        AddToQueue(msg);
    }

    private void AddToQueue(SimulationTelemetry telemetry)
    {
        _history.Enqueue(telemetry);

        _lastTimestamp = telemetry.Timestamp;

        while (_history.Count > MaxHistorySize) _history.TryDequeue(out _);
    }

    private async Task LoadHistoryFromDb(long runId)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HistoryDbContext>();

        var dbData = await db.Telemetry
            .Where(t => t.RunId == runId)
            .OrderByDescending(t => t.Timestamp)
            .Take(MaxHistorySize)
            .ToListAsync();

        if (dbData.Count > 0)
        {
            var mappedData = dbData
                .OrderBy(t => t.Timestamp)
                .Select(e => new SimulationTelemetry
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

            foreach (var item in mappedData) _history.Enqueue(item);
        }
    }

    public List<SimulationTelemetry> GetHistory()
    {
        return _history.ToList();
    }
}