using System.Globalization;
using System.Net.Http.Json;
using WeatherForecast.Service.Models;

namespace WeatherForecast.Service.Services;

public class OpenMeteoClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly ILogger<OpenMeteoClient> _logger;

    public OpenMeteoClient(HttpClient http, IConfiguration config, ILogger<OpenMeteoClient> logger)
    {
        _http = http;
        _baseUrl = config["WeatherService:OpenMeteoBaseUrl"]
                   ?? "https://api.open-meteo.com/v1/forecast";
        _logger = logger;
    }

    public async Task<CurrentWeatherData?> GetCurrentWeatherAsync(
        decimal latitude, decimal longitude, CancellationToken ct)
    {
        var url = string.Format(
            CultureInfo.InvariantCulture,
            "{0}?latitude={1}&longitude={2}&current=temperature_2m,precipitation,weather_code&timezone=Europe%2FBrussels",
            _baseUrl,
            latitude,
            longitude);

        _logger.LogDebug("Calling Open-Meteo: {Url}", url);

        var response = await _http.GetFromJsonAsync<OpenMeteoResponse>(url, ct);
        return response?.Current;
    }
}
