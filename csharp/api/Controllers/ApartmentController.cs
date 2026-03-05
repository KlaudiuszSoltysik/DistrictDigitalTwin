using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using shared;

namespace api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApartmentController(ILogger<ApartmentController> logger, IMongoDatabase mongodb) : ControllerBase
{
    [HttpPost("hvac-control")]
    public async Task<IActionResult> HvacControl([FromBody] HvacControl command)
    {
        var collection = mongodb.GetCollection<BsonDocument>("apartments-config");

        var filter = Builders<BsonDocument>.Filter.Eq("apartment_id", command.ApartmentId);

        var updateDefinitions = command.HvacRoomControls.Select(roomControl =>
            Builders<BsonDocument>.Update.Set($"{roomControl.RoomId}.hvac.temperatures",
                roomControl.Temperatures.Select(t => double.Clamp(t, 16.0, 26.0)))
        ).ToList();

        var combinedUpdate = Builders<BsonDocument>.Update.Combine(updateDefinitions);

        var options = new UpdateOptions { IsUpsert = true };

        await collection.UpdateOneAsync(filter, combinedUpdate, options);

        return Ok();
    }

    [HttpGet("get-room-list")]
    public async Task<IActionResult> GetRoomList([FromQuery] string building, string apartment)
    {
        var collection = mongodb.GetCollection<BsonDocument>("district-config");

        var document = await collection.Find(new BsonDocument()).FirstOrDefaultAsync();

        var targetBuilding = document["buildings"].AsBsonArray
            .FirstOrDefault(b => b["id"].AsString == building);

        if (targetBuilding == null) return NotFound($"Building not found: {building}");

        var targetApartment = targetBuilding["apartments"].AsBsonArray
            .FirstOrDefault(a => a["id"].AsString == apartment);

        if (targetApartment == null) return NotFound($"Apartment not found: {apartment}");

        var roomList = targetApartment["rooms"].AsBsonArray
            .Select(r => new RoomInformation
            {
                Id = r["id"].AsString,
                Name = r["name"].AsString
            }).ToList();

        return Ok(roomList);
    }
}