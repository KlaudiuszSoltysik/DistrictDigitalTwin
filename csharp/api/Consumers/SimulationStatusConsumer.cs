using MassTransit;
using Microsoft.AspNetCore.SignalR;
using shared;

namespace api.Consumers;

public class SimulationStatusConsumer(IHubContext<TelemetryHub> hubContext) : IConsumer<SimulationConfig>
{
    public async Task Consume(ConsumeContext<SimulationConfig> context)
    {
        await hubContext.Clients.All.SendAsync("ReceiveSimulationStatus", context.Message);
    }
}