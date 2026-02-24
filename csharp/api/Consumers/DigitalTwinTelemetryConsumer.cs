using MassTransit;
using Microsoft.AspNetCore.SignalR;
using shared;

namespace api.Consumers;

public class DigitalTwinTelemetryConsumer(
    IHubContext<TelemetryHub> simulationHubContext,
    CacheService cacheService,
    ILogger<DigitalTwinTelemetryConsumer> logger)
    : IConsumer<Telemetry[]>
{
    public async Task Consume(ConsumeContext<Telemetry[]> context)
    {
        List<Telemetry> msgs;

        try
        {
            msgs = context.Message.ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse and process message. Method: {method}", "Consume");
            return;
        }

        await cacheService.ProcessDigitalTwinTelemetryMessageAsync(msgs);
        await simulationHubContext.Clients.All.SendAsync("ReceiveDigitalTwinTelemetry", msgs);
    }
}