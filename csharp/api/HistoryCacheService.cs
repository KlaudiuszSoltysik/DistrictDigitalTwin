using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using shared;

namespace api;

public class HistoryCacheService(IServiceScopeFactory scopeFactory)
{
    private const int MaxHistorySize = 86400;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private long _currentRunId = -1;

    private ConcurrentQueue<SimulationTelemetry> _history = new();

    private DateTime _lastTimestamp = DateTime.MinValue;

    public async Task ProcessMessageAsync(SimulationTelemetry msg)
    {
        msg.Timestamp = msg.Timestamp.Kind switch
        {
            DateTimeKind.Local => msg.Timestamp.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(msg.Timestamp, DateTimeKind.Utc),
            _ => msg.Timestamp
        };

        var cutoffTime = msg.Timestamp.AddHours(-24);

        if (msg.RunId != _currentRunId)
        {
            await _lock.WaitAsync();
            try
            {
                if (msg.RunId != _currentRunId)
                {
                    _history = new ConcurrentQueue<SimulationTelemetry>();

                    await LoadHistoryFromDb(msg.RunId, cutoffTime);

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

        if (msg.Timestamp <= _lastTimestamp) return;

        _history.Enqueue(msg);

        TrimOldHistory(cutoffTime);
    }

    private void TrimOldHistory(DateTime cutoff)
    {
        while (_history.TryPeek(out var oldestItem) && oldestItem.Timestamp < cutoff)
        {
            _history.TryDequeue(out _);
        }
    }

    private async Task LoadHistoryFromDb(long runId, DateTime cutoff)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HistoryDbContext>();

        var dbData = await db.Telemetry
            .Where(t => t.RunId == runId && t.Timestamp >= cutoff)
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