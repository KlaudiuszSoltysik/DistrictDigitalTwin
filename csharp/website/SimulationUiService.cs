using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using shared;

namespace website;

public class SimulationUiService
{
    private readonly HttpClient _httpClient;
    private readonly HubConnection _hubConnection;

    public SimulationUiService(HubConnection hubConnection, HttpClient httpClient)
    {
        _hubConnection = hubConnection;
        _httpClient = httpClient;

        _hubConnection.On<SimulationStatus>("ReceiveSimulationStatus", status =>
        {
            CurrentStatus = status;
            OnStatusChanged?.Invoke();
        });

        _ = EnsureConnectionStarted();
    }

    public SimulationStatus? CurrentStatus { get; private set; }

    public event Action? OnStatusChanged;

    public async Task SendCommandAsync(string action, SimulationConfig? targetConfig = null)
    {
        var command = new ControlMessage
        {
            Action = action,
            TargetConfig = targetConfig
        };

        await _httpClient.PostAsJsonAsync("api/simulation/control", command);
    }

    private async Task EnsureConnectionStarted()
    {
        if (_hubConnection.State == HubConnectionState.Disconnected)
            try
            {
                await _hubConnection.StartAsync();
                Console.WriteLine("[SIGNALR] Połączono pomyślnie!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SIGNALR ERROR] Nie udało się połączyć: {ex.Message}");
            }
    }
}