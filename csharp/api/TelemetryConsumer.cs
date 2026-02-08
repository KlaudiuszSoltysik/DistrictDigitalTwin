using MassTransit;
using Microsoft.AspNetCore.SignalR;
using shared;

namespace api;

public class TelemetryConsumer(IHubContext<SimulationHub> simulationHubContext, HistoryCacheService historyCacheService)
    : IConsumer<SimulationTelemetry>
{
    public async Task Consume(ConsumeContext<SimulationTelemetry> context)
    {
        var msg = context.Message;

        await historyCacheService.ProcessMessageAsync(msg);

        await simulationHubContext.Clients.All.SendAsync("ReceiveSimulationData", msg);
    }
}