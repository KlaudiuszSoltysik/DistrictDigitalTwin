using Microsoft.AspNetCore.SignalR;

namespace api;

public class TelemetryHub(CacheService cacheService) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var simulationTelemetry = cacheService.GetSimulationTelemetry();

        if (simulationTelemetry.Count != 0)
            await Clients.Caller.SendAsync("ReceiveSimulationTelemetryDb", simulationTelemetry);

        var monthlySimulationTelemetry = cacheService.GetMonthlySimulationTelemetry();

        if (monthlySimulationTelemetry.Count != 0)
            await Clients.Caller.SendAsync("ReceiveMonthlyTelemetryDb", monthlySimulationTelemetry);

        var digitalTwinTelemetry = cacheService.GetDigitalTwinTelemetry();

        if (digitalTwinTelemetry.Count != 0)
            await Clients.Caller.SendAsync("ReceiveDigitalTwinTelemetry", digitalTwinTelemetry);

        await base.OnConnectedAsync();
    }
}