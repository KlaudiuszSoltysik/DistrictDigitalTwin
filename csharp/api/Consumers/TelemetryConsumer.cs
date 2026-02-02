using backend.Contracts;
using backend.Hubs;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace backend.Consumers;

public class TelemetryConsumer(IHubContext<SimulationHub> hubContext) : IConsumer<SimulationTelemetry>
{
    public async Task Consume(ConsumeContext<SimulationTelemetry> context)
    {
        var msg = context.Message;

        Console.WriteLine($"[RabbitMQ] Received: timestamp: {msg.Timestamp}");

        await hubContext.Clients.All.SendAsync("ReceiveSimulationData", msg);
    }
}