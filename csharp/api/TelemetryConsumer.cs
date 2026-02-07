using MassTransit;
using Microsoft.AspNetCore.SignalR;
using shared;

namespace api;

public class TelemetryConsumer(IHubContext<SimulationHub> hubContext, HistoryCacheService historyCacheService)
    : IConsumer<SimulationTelemetry>
{
    public async Task Consume(ConsumeContext<SimulationTelemetry> context)
    {
        var msg = context.Message;

        historyCacheService.Add(msg);

        await hubContext.Clients.All.SendAsync("ReceiveSimulationData", msg);
    }
}