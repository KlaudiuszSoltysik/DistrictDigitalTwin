using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using shared;

namespace website;

public class SimulationStatusService : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly HubConnection _hubConnection;

    public SimulationStatusService(HubConnection hubConnection, HttpClient httpClient)
    {
        SimulationTimestamp = DateTimeOffset.UtcNow;
        DigitalTwinTimestamp = DateTimeOffset.UtcNow;

        _hubConnection = hubConnection;
        _httpClient = httpClient;

        _hubConnection.On<SimulationConfig>("ReceiveSimulationStatus", status =>
        {
            CurrentStatus = status;
            OnStatusChanged?.Invoke();
        });

        _hubConnection.On<Telemetry>("ReceiveSimulationTelemetry", msg =>
        {
            SimulationTimestamp = msg.Timestamp;
            SimulationTelemetry.Add(msg);

            var cutoffTime = msg.Timestamp.AddHours(-24);
            SimulationTelemetry.RemoveAll(t => t.Timestamp < cutoffTime);

            SimulationTelemetry = SimulationTelemetry
                .GroupBy(t => t.Timestamp.ToUnixTimeSeconds() / 60)
                .Select(group => group.Last())
                .OrderBy(t => t.Timestamp)
                .ToList();

            OnSimulationTelemetryReceived?.Invoke(msg);
        });

        _hubConnection.On<List<Telemetry>>("ReceiveSimulationTelemetryDb", msgs =>
        {
            if (msgs.Count > 0) SimulationTimestamp = msgs[0].Timestamp;

            SimulationTelemetry = msgs;
            OnSimulationTelemetryDbReceived?.Invoke(msgs);
        });

        _hubConnection.On<List<Telemetry>>("ReceiveDigitalTwinTelemetry", msg =>
        {
            DigitalTwinTimestamp = msg[0].Timestamp;

            foreach (var incomingItem in msg)
            {
                var existingIndex = DigitalTwinTelemetry.FindIndex(t => t.Timestamp == incomingItem.Timestamp);

                if (existingIndex >= 0)
                    DigitalTwinTelemetry[existingIndex] = incomingItem;
                else
                    DigitalTwinTelemetry.Add(incomingItem);
            }

            DigitalTwinTelemetry = DigitalTwinTelemetry.OrderBy(t => t.Timestamp).ToList();
            OnDigitalTwinTelemetryReceived?.Invoke(DigitalTwinTelemetry);
        });

        _ = EnsureConnectionStarted();
    }

    public List<Telemetry> SimulationTelemetry { get; private set; } = [];
    public List<Telemetry> DigitalTwinTelemetry { get; private set; } = [];

    public DateTimeOffset SimulationTimestamp { get; private set; }
    public DateTimeOffset DigitalTwinTimestamp { get; private set; }

    public SimulationConfig? CurrentStatus { get; private set; }

    public async ValueTask DisposeAsync()
    {
        _hubConnection.Remove("ReceiveSimulationStatus");
        _hubConnection.Remove("ReceiveSimulationTelemetry");
        _hubConnection.Remove("ReceiveSimulationTelemetryDb");
        _hubConnection.Remove("ReceiveDigitalTwinTelemetry");

        await _hubConnection.StopAsync();
        await _hubConnection.DisposeAsync();
    }

    public event Action? OnStatusChanged;
    public event Action<Telemetry>? OnSimulationTelemetryReceived;
    public event Action<List<Telemetry>>? OnSimulationTelemetryDbReceived;
    public event Action<List<Telemetry>>? OnDigitalTwinTelemetryReceived;

    public void ClearTelemetry()
    {
        SimulationTelemetry.Clear();
        DigitalTwinTelemetry.Clear();
        SimulationTimestamp = DateTimeOffset.UtcNow;
        DigitalTwinTimestamp = DateTimeOffset.UtcNow;
    }

    public async Task SendCommandAsync(string action, string? deviceName = null, Config? targetConfig = null)
    {
        var command = new ControlMessage
        {
            Action = action,
            TargetName = deviceName,
            TargetConfig = targetConfig
        };

        await _httpClient.PostAsJsonAsync("simulation/control", command);
    }

    private async Task EnsureConnectionStarted()
    {
        if (_hubConnection.State == HubConnectionState.Disconnected)
            try
            {
                await _hubConnection.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
    }
}