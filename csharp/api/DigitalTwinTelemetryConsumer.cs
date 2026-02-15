using MassTransit;
using Microsoft.AspNetCore.SignalR;
using shared;

namespace api;

public class DigitalTwinTelemetryConsumer(IHubContext<TelemetryHub> simulationHubContext, CacheService cacheService)
    : IConsumer<Telemetry[]>
{
    public async Task Consume(ConsumeContext<Telemetry[]> context)
    {
        var messageArray = context.Message;

        var msgList = messageArray.ToList();

        await cacheService.ProcessDigitalTwinTelemetryMessageAsync(msgList);

        await simulationHubContext.Clients.All.SendAsync("ReceiveDigitalTwinTelemetry", msgList);
    }
}