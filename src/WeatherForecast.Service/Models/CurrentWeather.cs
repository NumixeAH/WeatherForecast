using System.Text.Json.Serialization;

namespace WeatherForecast.Service.Models;

public class OpenMeteoResponse
{
    [JsonPropertyName("current")]
    public CurrentWeatherData? Current { get; set; }
}

public class CurrentWeatherData
{
    [JsonPropertyName("temperature_2m")]
    public decimal Temperature { get; set; }

    [JsonPropertyName("precipitation")]
    public decimal Precipitation { get; set; }

    [JsonPropertyName("weather_code")]
    public int WeatherCode { get; set; }
}
