using MassTransit;
using Microsoft.AspNetCore.SignalR;
using shared;

namespace api;

public class DigitalTwinConsumer(IHubContext<SimulationHub> simulationHubContext, CacheService cacheService)
    : IConsumer<List<Telemetry>>
{
    public async Task Consume(ConsumeContext<List<Telemetry>> context)
    {
        var msg = context.Message;

        await cacheService.ProcessDigitalTwinTelemetryMessageAsync(msg);

        await simulationHubContext.Clients.All.SendAsync("ReceiveDigitalTwinTelemetry", msg);
    }
}