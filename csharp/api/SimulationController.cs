using MassTransit;
using Microsoft.AspNetCore.Mvc;
using shared;

namespace api;

[ApiController]
[Route("api/[controller]")]
public class SimulationController(ISendEndpointProvider sendEndpointProvider) : ControllerBase
{
    [HttpPost("control")]
    public async Task<IActionResult> SendControlMessage([FromBody] ControlMessage command)
    {
        if (string.IsNullOrEmpty(command.Action))
            return BadRequest("Action is required.");

        var endpoint = await sendEndpointProvider.GetSendEndpoint(new Uri("queue:commands"));

        await endpoint.Send(command);

        return Ok(new { message = "Command sent", command });
    }
}