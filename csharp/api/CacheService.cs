using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using shared;

namespace api;

public class CacheService(IServiceScopeFactory scopeFactory, ILogger<CacheService> logger)
{
    private readonly SemaphoreSlim _digitalTwinLock = new(1, 1);
    private readonly ConcurrentQueue<Telemetry> _digitalTwinTelemetry = new();
    private readonly ConcurrentQueue<Telemetry> _monthlySimulationTelemetry = new();
    private readonly SemaphoreSlim _simulationLock = new(1, 1);

    private readonly ConcurrentQueue<Telemetry> _simulationTelemetry = new();
    private long _currentDigitalTwinRunId = -1;

    private long _currentSimulationRunId = -1;

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
            var monthlyCutoff = GetBeginningOfPreviousMonth(latestSim.Timestamp);

            await LoadSimulationTelemetryFromDb(_simulationTelemetry, latestSim.RunId, cutoff);
            await LoadSimulationTelemetryFromDb(_monthlySimulationTelemetry, latestSim.RunId, monthlyCutoff);
        }

        var latestTwin = await db.DigitalTwinTelemetry
            .AsNoTracking()
            .OrderByDescending(t => t.Timestamp)
            .FirstOrDefaultAsync();

        if (latestTwin != null)
        {
            _currentDigitalTwinRunId = 1;
            var cutoff = latestTwin.Timestamp.AddHours(-48);
            await LoadDigitalTwinTelemetryFromDb(cutoff);
        }
    }

    public async Task ProcessSimulationTelemetryMessageAsync(Telemetry msg)
    {
        var cutoff = msg.Timestamp.AddHours(-24);
        var monthlyCutoff = GetBeginningOfPreviousMonth(msg.Timestamp);

        if (msg.RunId != _currentSimulationRunId)
        {
            _simulationTelemetry.Clear();
            _monthlySimulationTelemetry.Clear();

            await LoadSimulationTelemetryFromDb(_simulationTelemetry, msg.RunId, cutoff);
            await LoadSimulationTelemetryFromDb(_monthlySimulationTelemetry, msg.RunId, monthlyCutoff);

            _currentSimulationRunId = msg.RunId;
        }

        _simulationTelemetry.Enqueue(msg);
        _monthlySimulationTelemetry.Enqueue(msg);

        while (_simulationTelemetry.TryPeek(out var oldest) && oldest.Timestamp < cutoff)
            _simulationTelemetry.TryDequeue(out _);

        while (_monthlySimulationTelemetry.TryPeek(out var oldest) && oldest.Timestamp < monthlyCutoff)
            _monthlySimulationTelemetry.TryDequeue(out _);
    }

    private async Task LoadSimulationTelemetryFromDb(ConcurrentQueue<Telemetry> telemetryQueue, long runId,
        DateTimeOffset cutoff)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

        var dbData = await db.SimulationTelemetry
            .AsNoTracking()
            .Where(t => t.RunId == runId && t.Timestamp >= cutoff)
            .OrderBy(t => t.Timestamp)
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
                        SunRadiation = e.SunRadiation,
                        Co2 = e.Co2
                    },
                    EnergyCosts = new EnergyCostsData
                    {
                        PvFarmYield = e.PvFarmYield,
                        CopHeating = e.CopHeating,
                        CopCooling = e.CopCooling
                    },
                    RoomTemperatures = JsonSerializer.Deserialize<Dictionary<string, double>>(e.RoomTemperatures) ??
                                       new Dictionary<string, double>(),
                    RoomCo2 = JsonSerializer.Deserialize<Dictionary<string, int>>(e.RoomCo2) ??
                              new Dictionary<string, int>(),
                    RoomHvacQ = JsonSerializer.Deserialize<Dictionary<string, double>>(e.RoomHvacQ) ??
                                new Dictionary<string, double>(),
                    RoomHeatings = JsonSerializer.Deserialize<Dictionary<string, double>>(e.RoomHeatings) ??
                                   new Dictionary<string, double>(),
                    RoomHvacV = JsonSerializer.Deserialize<Dictionary<string, double>>(e.RoomHvacV) ??
                                new Dictionary<string, double>(),
                    Metering = JsonSerializer.Deserialize<MeteringData>(e.Metering) ??
                               new MeteringData()
                });

            foreach (var item in mappedData) telemetryQueue.Enqueue(item);
        }
    }

    public async Task ProcessDigitalTwinTelemetryMessageAsync(List<Telemetry> msgs)
    {
        var cutoff = msgs[0].Timestamp.AddHours(-24);

        if (_currentDigitalTwinRunId == -1)
        {
            _digitalTwinTelemetry.Clear();

            await LoadDigitalTwinTelemetryFromDb(cutoff);

            _currentDigitalTwinRunId = 1;
        }

        foreach (var msg in msgs)
            _digitalTwinTelemetry.Enqueue(msg);

        while (_digitalTwinTelemetry.TryPeek(out var oldest) && oldest.Timestamp < cutoff)
            _digitalTwinTelemetry.TryDequeue(out _);
    }

    private async Task LoadDigitalTwinTelemetryFromDb(DateTimeOffset cutoffTime)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

        var dbData = await db.DigitalTwinTelemetry
            .AsNoTracking()
            .Where(t => t.Timestamp >= cutoffTime)
            .OrderBy(t => t.Timestamp)
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
                        SunRadiation = e.SunRadiation,
                        Co2 = e.Co2
                    },
                    EnergyCosts = new EnergyCostsData
                    {
                        PvFarmYield = e.PvFarmYield,
                        CopHeating = e.CopHeating,
                        CopCooling = e.CopCooling
                    },
                    RoomTemperatures = JsonSerializer.Deserialize<Dictionary<string, double>>(e.RoomTemperatures) ??
                                       new Dictionary<string, double>(),
                    RoomCo2 = JsonSerializer.Deserialize<Dictionary<string, int>>(e.RoomCo2) ??
                              new Dictionary<string, int>(),
                    RoomHvacQ = JsonSerializer.Deserialize<Dictionary<string, double>>(e.RoomHvacQ) ??
                                new Dictionary<string, double>(),
                    RoomHeatings = JsonSerializer.Deserialize<Dictionary<string, double>>(e.RoomHeatings) ??
                                   new Dictionary<string, double>(),
                    RoomHvacV = JsonSerializer.Deserialize<Dictionary<string, double>>(e.RoomHvacV) ??
                                new Dictionary<string, double>(),
                    Metering = JsonSerializer.Deserialize<MeteringData>(e.Metering) ??
                               new MeteringData()
                });

            foreach (var item in mappedData) _digitalTwinTelemetry.Enqueue(item);
        }
    }

    public List<Telemetry> GetSimulationTelemetry()
    {
        return _simulationTelemetry.ToList();
    }

    public List<Telemetry> GetMonthlySimulationTelemetry()
    {
        return _monthlySimulationTelemetry.ToList();
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

            _simulationTelemetry.Clear();
            _monthlySimulationTelemetry.Clear();
            _digitalTwinTelemetry.Clear();

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

            await db.SimulationTelemetry.ExecuteDeleteAsync();
            await db.DigitalTwinTelemetry.ExecuteDeleteAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error. Method: {method}", "ClearDataAndCacheAsync");
        }
        finally
        {
            _simulationLock.Release();
            _digitalTwinLock.Release();
        }
    }

    private static DateTimeOffset GetBeginningOfPreviousMonth(DateTimeOffset current)
    {
        var prevMonth = current.AddMonths(-1);

        return new DateTimeOffset(prevMonth.Year, prevMonth.Month, 1, 0, 0, 0, current.Offset);
    }
}