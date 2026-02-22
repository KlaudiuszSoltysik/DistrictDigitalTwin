using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using shared;

namespace api;

public class CacheService(IServiceScopeFactory scopeFactory)
{
    private readonly SemaphoreSlim _digitalTwinLock = new(1, 1);
    private readonly SemaphoreSlim _simulationLock = new(1, 1);
    private long _currentDigitalTwinRunId = -1;

    private long _currentSimulationRunId = -1;
    private ConcurrentQueue<Telemetry> _digitalTwinTelemetry = new();

    private ConcurrentQueue<Telemetry> _simulationTelemetry = new();

    public async Task InitializeCacheAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

        var latestSim = await db.SimulationTelemetry
            .AsNoTracking()
            .OrderByDescending(t => t.Timestamp)
            .FirstOrDefaultAsync();

        if (latestSim != null)
        {
            _currentSimulationRunId = latestSim.RunId;
            var cutoff = latestSim.Timestamp.AddHours(-24);
            await LoadSimulationTelemetryFromDb(latestSim.RunId, cutoff);
        }

        var latestTwin = await db.DigitalTwinTelemetry
            .AsNoTracking()
            .OrderByDescending(t => t.Timestamp)
            .FirstOrDefaultAsync();

        if (latestTwin != null)
        {
            _currentDigitalTwinRunId = latestTwin.RunId;
            var cutoff = latestTwin.Timestamp.AddHours(-24);
            await LoadDigitalTwinTelemetryFromDb(cutoff);
        }
    }

    public async Task ProcessSimulationTelemetryMessageAsync(Telemetry msg)
    {
        var cutoffTime = msg.Timestamp.AddHours(-24);

        if (msg.RunId != _currentSimulationRunId)
        {
            await _simulationLock.WaitAsync();
            try
            {
                if (msg.RunId != _currentSimulationRunId)
                {
                    _simulationTelemetry = new ConcurrentQueue<Telemetry>();

                    await LoadSimulationTelemetryFromDb(msg.RunId, cutoffTime);

                    _currentSimulationRunId = msg.RunId;
                }
            }
            finally
            {
                _simulationLock.Release();
            }
        }

        _simulationTelemetry.Enqueue(msg);

        while (_simulationTelemetry.TryPeek(out var oldestItem) && oldestItem.Timestamp < cutoffTime)
            _simulationTelemetry.TryDequeue(out _);
    }

    private async Task LoadSimulationTelemetryFromDb(long runId, DateTimeOffset cutoffTime)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

        var dbData = await db.SimulationTelemetry
            .AsNoTracking()
            .Where(t => t.RunId == runId && t.Timestamp >= cutoffTime)
            .OrderByDescending(t => t.Timestamp)
            .ToListAsync();

        if (dbData.Count > 0)
        {
            var mappedData = dbData
                .OrderBy(t => t.Timestamp)
                .Select(e => new Telemetry
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

            foreach (var item in mappedData) _simulationTelemetry.Enqueue(item);
        }
    }

    public async Task ProcessDigitalTwinTelemetryMessageAsync(List<Telemetry> msgs)
    {
        var cutoffTime = msgs[0].Timestamp.AddHours(-24);

        if (_currentDigitalTwinRunId == -1)
        {
            await _digitalTwinLock.WaitAsync();
            try
            {
                if (_currentDigitalTwinRunId == -1)
                {
                    _digitalTwinTelemetry = new ConcurrentQueue<Telemetry>();

                    await LoadDigitalTwinTelemetryFromDb(cutoffTime);

                    _currentDigitalTwinRunId = 1;
                }
            }
            finally
            {
                _digitalTwinLock.Release();
            }
        }

        foreach (var msg in msgs)
            _digitalTwinTelemetry.Enqueue(msg);

        while (_digitalTwinTelemetry.TryPeek(out var oldestItem) && oldestItem.Timestamp < cutoffTime)
            _digitalTwinTelemetry.TryDequeue(out _);
    }

    private async Task LoadDigitalTwinTelemetryFromDb(DateTimeOffset cutoffTime)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

        var dbData = await db.DigitalTwinTelemetry
            .AsNoTracking()
            .Where(t => t.Timestamp >= cutoffTime)
            .OrderByDescending(t => t.Timestamp)
            .ToListAsync();

        if (dbData.Count > 0)
        {
            var mappedData = dbData
                .Select(e => new Telemetry
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

            foreach (var item in mappedData) _digitalTwinTelemetry.Enqueue(item);
        }
    }

    public List<Telemetry> GetSimulationTelemetry()
    {
        return _simulationTelemetry.ToList();
    }

    public List<Telemetry> GetDigitalTwinTelemetry()
    {
        return _digitalTwinTelemetry.ToList();
    }

    public async Task ClearDataAndCacheAsync()
    {
        await _simulationLock.WaitAsync();
        await _digitalTwinLock.WaitAsync();
        try
        {
            _currentSimulationRunId = -1;
            _currentDigitalTwinRunId = -1;

            _simulationTelemetry = new ConcurrentQueue<Telemetry>();
            _digitalTwinTelemetry = new ConcurrentQueue<Telemetry>();

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

            await db.SimulationTelemetry.ExecuteDeleteAsync();
            await db.DigitalTwinTelemetry.ExecuteDeleteAsync();
        }
        finally
        {
            _simulationLock.Release();
            _digitalTwinLock.Release();
        }
    }
}