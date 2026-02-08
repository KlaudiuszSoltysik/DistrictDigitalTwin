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

    public async Task ProcessMessageAsync(SimulationTelemetry msg)
    {
        if (msg.RunId != _currentRunId)
        {
            await _lock.WaitAsync();
            try
            {
                _history = new ConcurrentQueue<SimulationTelemetry>();

                await LoadHistoryFromDb(msg.RunId);

                _currentRunId = msg.RunId;
            }
            finally
            {
                _lock.Release();
            }
        }

        AddToQueue(msg);
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
                    Timestamp = e.Timestamp,
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

    private void AddToQueue(SimulationTelemetry telemetry)
    {
        _history.Enqueue(telemetry);
        while (_history.Count > MaxHistorySize) _history.TryDequeue(out _);
    }

    public List<SimulationTelemetry> GetHistory()
    {
        return _history.ToList();
    }
}