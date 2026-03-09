using MassTransit;
using Microsoft.AspNetCore.Mvc;
using shared;

namespace api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SimulationController(
    ISendEndpointProvider sendEndpointProvider,
    CacheService cacheService,
    ILogger<SimulationController> logger) : ControllerBase
{
    [HttpPost("control")]
    public async Task<IActionResult> Control([FromBody] ControlMessage command)
    {
        if (string.IsNullOrEmpty(command.Action))
        {
            logger.LogError("Invalid control message. Payload: {@payload}", command);
            return BadRequest("Action is required.");
        }

        if (command.Action.Equals("RESET", StringComparison.OrdinalIgnoreCase))
            await cacheService.ClearDataAndCacheAsync();

        var endpoint = await sendEndpointProvider.GetSendEndpoint(new Uri("queue:simulation-commands"));

        await endpoint.Send(command);

        return Ok();
    }
}