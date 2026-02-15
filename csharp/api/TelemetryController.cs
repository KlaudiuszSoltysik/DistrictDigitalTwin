using MassTransit;
using Microsoft.AspNetCore.Mvc;
using shared;

namespace api;

[ApiController]
[Route("api/[controller]")]
public class TelemetryController(ISendEndpointProvider sendEndpointProvider, CacheService cacheService) : ControllerBase
{
    [HttpPost("simulation-commands")]
    public async Task<IActionResult> SendControlMessage([FromBody] ControlMessage command)
    {
        if (string.IsNullOrEmpty(command.Action))
            return BadRequest("Action is required.");

        if (command.Action.Equals("RESET", StringComparison.OrdinalIgnoreCase))
            await cacheService.ClearDataAndCacheAsync();

        var endpoint = await sendEndpointProvider.GetSendEndpoint(new Uri("queue:simulation-commands"));

        await endpoint.Send(command);

        return Ok(new { message = "Command sent", command });
    }
}