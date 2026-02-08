using MassTransit;
using Microsoft.AspNetCore.SignalR;
using shared;

namespace api;

public class SimulationStatusConsumer(IHubContext<SimulationHub> hubContext) : IConsumer<SimulationStatus>
{
    public async Task Consume(ConsumeContext<SimulationStatus> context)
    {
        var status = context.Message;

        await hubContext.Clients.All.SendAsync("ReceiveSimulationStatus", status);
    }
}