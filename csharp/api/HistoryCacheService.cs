using System.Collections.Concurrent;
using shared;

namespace api;

public class HistoryCacheService
{
    private const int MaxHistorySize = 2000;
    private readonly ConcurrentQueue<SimulationTelemetry> _history = new();

    public void Add(SimulationTelemetry telemetry)
    {
        _history.Enqueue(telemetry);

        while (_history.Count > MaxHistorySize) _history.TryDequeue(out _);
    }

    public List<SimulationTelemetry> GetHistory()
    {
        return _history.ToList();
    }
}