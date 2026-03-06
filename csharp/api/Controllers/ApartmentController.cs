using MassTransit;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using shared;

namespace api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApartmentController(
    ISendEndpointProvider sendEndpointProvider,
    ILogger<ApartmentController> logger,
    IMongoDatabase mongodb) : ControllerBase
{
    [HttpPost("update-apartment-config")]
    public async Task<IActionResult> UpdateApartmentConfig([FromBody] ApartmentConfig incomingConfig)
    {
        var apartmentCollection = mongodb.GetCollection<ApartmentConfig>("apartments-config");

        var existingConfig = await apartmentCollection
            .Find(a => a.BuildingId == incomingConfig.BuildingId && a.ApartmentId == incomingConfig.ApartmentId)
            .SingleOrDefaultAsync();

        if (existingConfig != null)
        {
            foreach (var existingRoom in existingConfig.Rooms)
            {
                var incomingRoom = incomingConfig.Rooms.FirstOrDefault(r => r.Id == existingRoom.Id);
                if (incomingRoom == null) continue;

                existingRoom.HvacControl.Temperatures = incomingRoom.HvacControl.Temperatures
                    .Select(t => double.Clamp(t, 16.0, 26.0))
                    .ToList();

                existingRoom.HvacControl.Tolerance = double.Clamp(incomingRoom.HvacControl.Tolerance, 0.1, 10.0);

                existingRoom.HvacControl.IsEnabled = incomingRoom.HvacControl.IsEnabled;
            }

            var filter = Builders<ApartmentConfig>.Filter.Eq(d => d.BuildingId, existingConfig.BuildingId)
                         & Builders<ApartmentConfig>.Filter.Eq(d => d.ApartmentId, existingConfig.ApartmentId);

            await apartmentCollection.ReplaceOneAsync(filter, existingConfig);
        }
        else
        {
            var districtCollection = mongodb.GetCollection<BsonDocument>("district-config");
            var districtDocument = await districtCollection.Find(new BsonDocument()).FirstOrDefaultAsync();

            var targetApartment = districtDocument?["buildings"].AsBsonArray
                .FirstOrDefault(b => b["id"].AsString == incomingConfig.BuildingId)?["apartments"].AsBsonArray
                .FirstOrDefault(a => a["id"].AsString == incomingConfig.ApartmentId);

            if (targetApartment == null) return BadRequest("Wrong building or apartment.");

            var newConfig = new ApartmentConfig
            {
                BuildingId = incomingConfig.BuildingId,
                ApartmentId = incomingConfig.ApartmentId,
                Rooms = targetApartment["rooms"].AsBsonArray.Select(r =>
                {
                    var roomId = r["id"].AsString;
                    var userRoom = incomingConfig.Rooms.FirstOrDefault(ur => ur.Id == roomId);
                    var userTemperatures = userRoom?.HvacControl.Temperatures;
                    var userTolerance = userRoom?.HvacControl.Tolerance;
                    var userIsEnabled = userRoom?.HvacControl.IsEnabled;

                    return new RoomConfig
                    {
                        Id = roomId,
                        Name = r["name"].AsString,
                        HvacControl = new HvacControl
                        {
                            Temperatures = userTemperatures is { Count: 24 }
                                ? userTemperatures.Select(t => double.Clamp(t, 16.0, 26.0)).ToList()
                                : Enumerable.Repeat(21.0, 24).ToList(),

                            Tolerance = userTolerance.HasValue
                                ? double.Clamp(userTolerance.Value, 0.1, 10.0)
                                : 0.5,
                            IsEnabled = userIsEnabled ?? false
                        }
                    };
                }).ToList()
            };

            await apartmentCollection.InsertOneAsync(newConfig);
        }


        var endpoint = await sendEndpointProvider.GetSendEndpoint(new Uri("queue:simulation-commands"));

        await endpoint.Send(new ControlMessage
        {
            Action = "UPDATE_APARTMENT_CONFIG",
            TargetConfig = new Config
            {
                BuildingId = incomingConfig.BuildingId,
                ApartmentId = incomingConfig.ApartmentId
            }
        });

        return Ok();
    }

    [HttpGet("get-apartment-config")]
    public async Task<IActionResult> GetApartmentConfig([FromQuery] string building, [FromQuery] string apartment)
    {
        var apartmentCollection = mongodb.GetCollection<ApartmentConfig>("apartments-config");

        var response = await apartmentCollection
            .Find(a => a.BuildingId == building && a.ApartmentId == apartment)
            .SingleOrDefaultAsync();

        if (response != null) return Ok(response);

        var districtCollection = mongodb.GetCollection<BsonDocument>("district-config");
        var districtDocument = await districtCollection.Find(new BsonDocument()).FirstOrDefaultAsync();

        if (districtDocument == null) return NotFound("District config not found in DB.");

        var targetBuilding = districtDocument["buildings"].AsBsonArray
            .FirstOrDefault(b => b["id"].AsString == building);

        if (targetBuilding == null) return NotFound($"Building not found: {building}");

        var targetApartment = targetBuilding["apartments"].AsBsonArray
            .FirstOrDefault(a => a["id"].AsString == apartment);

        if (targetApartment == null) return NotFound($"Apartment not found: {apartment}");

        response = new ApartmentConfig
        {
            BuildingId = building,
            ApartmentId = apartment,
            Rooms = targetApartment["rooms"].AsBsonArray
                .Select(r => new RoomConfig
                {
                    Id = r["id"].AsString,
                    Name = r["name"].AsString,
                    HvacControl = new HvacControl
                    {
                        Temperatures = Enumerable.Repeat(21.0, 24).ToList(),
                        Tolerance = 0.1,
                        IsEnabled = false
                    }
                }).ToList()
        };

        return Ok(response);
    }
}