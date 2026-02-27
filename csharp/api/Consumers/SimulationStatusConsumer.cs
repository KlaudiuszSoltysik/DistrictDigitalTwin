using MassTransit;
using Microsoft.AspNetCore.SignalR;
using shared;

namespace api.Consumers;

public class SimulationStatusConsumer(IHubContext<TelemetryHub> hubContext) : IConsumer<SimulationStatus>
{
    public async Task Consume(ConsumeContext<SimulationStatus> context)
    {
        await hubContext.Clients.All.SendAsync("ReceiveSimulationStatus", context.Message);
    }
}