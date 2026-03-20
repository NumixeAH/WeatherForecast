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

// --- Historical / archive API ---

public class OpenMeteoArchiveResponse
{
    [JsonPropertyName("daily")]
    public ArchiveDailyData? Daily { get; set; }
}

public class ArchiveDailyData
{
    [JsonPropertyName("time")]
    public List<string> Time { get; set; } = [];

    [JsonPropertyName("temperature_2m_mean")]
    public List<decimal?> TemperatureMean { get; set; } = [];

    [JsonPropertyName("precipitation_sum")]
    public List<decimal?> PrecipitationSum { get; set; } = [];

    [JsonPropertyName("weather_code")]
    public List<int?> WeatherCode { get; set; } = [];
}

public record HistoricalWeatherDay(DateOnly Date, decimal Temperature, decimal Precipitation, int WeatherCode);
