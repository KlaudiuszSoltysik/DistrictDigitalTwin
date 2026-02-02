using System.Text.Json.Serialization;

namespace backend.Contracts;

public class SimulationTelemetry
{
    public DateTime Timestamp { get; init; }

    [JsonPropertyName("room_temps")]
    public Dictionary<string, double> RoomTemperatures { get; init; } = new();

    public required WeatherData Weather { get; init; }

}

public class WeatherData
{
    public double Temperature { get; init; }

    [JsonPropertyName("wind_speed")]
    public double WindSpeed { get; init; }

    [JsonPropertyName("wind_direction")]
    public double WindDirection { get; init; }

    [JsonPropertyName("sun_radiation")]
    public double SunRadiation { get; init; }

    [JsonPropertyName("sun_altitude")]
    public double SunAltitude { get; init; }

    [JsonPropertyName("sun_azimuth")]
    public double SunAzimuth { get; init; }
}