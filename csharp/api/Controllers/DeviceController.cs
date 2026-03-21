using MassTransit;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DeviceController(
    ISendEndpointProvider sendEndpointProvider,
    ILogger<DeviceController> logger,
    IMongoDatabase mongodb) : ControllerBase
{
    // [HttpPost("update-hvac-unit-config")]
    // public async Task<IActionResult> UpdateHvacConfig([FromBody] HvacUnitConfig incomingConfig)
    // {
    //     var collection = mongodb.GetCollection<HvacUnitConfig>("devices-config");
    //
    //     var configToSave = new HvacUnitConfig
    //     {
    //         Name = incomingConfig.Name,
    //         PBand = double.Clamp(incomingConfig.PBand, 1.0, 10.0),
    //         Ti = double.Clamp(incomingConfig.Ti, 1.0, 10.0) * 3600.0
    //     };
    //
    //     var filter = Builders<HvacUnitConfig>.Filter.Eq(c => c.Name, configToSave.Name);
    //     var options = new ReplaceOptions { IsUpsert = true };
    //
    //     await collection.ReplaceOneAsync(filter, configToSave, options);
    //
    //     var endpoint = await sendEndpointProvider.GetSendEndpoint(new Uri("queue:simulation-commands"));
    //     await endpoint.Send(new ControlMessage
    //     {
    //         Action = "UPDATE_HVAC_CONFIG"
    //     });
    //
    //     return Ok();
    // }
    //
    // [HttpGet("get-hvac-unit-config")]
    // public async Task<IActionResult> GetHvacConfig([FromQuery] string name = "hvac")
    // {
    //     var collection = mongodb.GetCollection<HvacUnitConfig>("devices-config");
    //     var filter = Builders<HvacUnitConfig>.Filter.Eq(c => c.Name, name);
    //
    //     var config = await collection.Find(filter).FirstOrDefaultAsync() ?? new HvacUnitConfig
    //     {
    //         Name = name,
    //         PBand = 2.0,
    //         Ti = 4.0 * 3600.0
    //     };
    //
    //     var configForFrontend = new HvacUnitConfig
    //     {
    //         Name = config.Name,
    //         PBand = config.PBand,
    //         Ti = config.Ti / 3600.0
    //     };
    //
    //     return Ok(configForFrontend);
    // }
}