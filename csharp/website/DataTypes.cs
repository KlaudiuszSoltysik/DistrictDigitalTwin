namespace website;

// public class WeatherDataPoint
// {
//     public DateTime Timestamp { get; set; }
//     public double Temp { get; set; }
//     public double WindSpeed { get; set; }
//     public double WindDir { get; set; }
//     public double SunRadiation { get; set; }
//     public double SunAltitude { get; set; }
//     public double SunAzimuth { get; set; }
// }
//
// public class TemperatureDataPoint
// {
//     public DateTime Timestamp { get; set; }
//     public double Value { get; set; }
// }
//
// public class WeatherData
// {
//     public required double Temp { get; set; }
//     public required double WindSpeed { get; set; }
//     public required double WindDir { get; set; }
//     public required double SunRadiation { get; set; }
//     public required double SunAltitude { get; set; }
//     public required double SunAzimuth { get; set; }
// }
//
// public class Telemetry
// {
//     public string Type { get; set; } = "Telemetry";
//     public required WeatherData Weather { get; set; }
//     public Dictionary<string, double> Temperatures { get; set; } = new();
//     public double SpeedFactor { get; set; }
//     public DateTime SimTimestamp { get; set; }
// }
//
// public class SimulationParameters
// {
//     public string Type { get; set; } = "SimulationParameters";
//     public bool IsAuto { get; set; }
//     public double SpeedFactor { get; set; }
//     public double Temp { get; set; }
//     public double WindSpeed { get; set; }
//     public double WindDir { get; set; }
//     public double SunRadiation { get; set; }
//     public double SunAltitude { get; set; }
//     public double SunAzimuth { get; set; }
// }