using MassTransit;
using Microsoft.AspNetCore.SignalR;
using shared;

namespace api;

public class SimulationTelemetryConsumer(IHubContext<SimulationHub> simulationHubContext, CacheService cacheService)
    : IConsumer<Telemetry>
{
    public async Task Consume(ConsumeContext<Telemetry> context)
    {
        var msg = context.Message;

        await cacheService.ProcessSimulationTelemetryMessageAsync(msg);

        await simulationHubContext.Clients.All.SendAsync("ReceiveSimulationTelemetry", msg);
    }
}