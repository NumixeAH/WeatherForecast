using System.Globalization;
using System.Net.Http.Json;
using WeatherForecast.Service.Models;

namespace WeatherForecast.Service.Services;

public class OpenMeteoClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _archiveBaseUrl;
    private readonly ILogger<OpenMeteoClient> _logger;

    public OpenMeteoClient(HttpClient http, IConfiguration config, ILogger<OpenMeteoClient> logger)
    {
        _http = http;
        _baseUrl = config["WeatherService:OpenMeteoBaseUrl"]
                   ?? "https://api.open-meteo.com/v1/forecast";
        _archiveBaseUrl = config["WeatherService:OpenMeteoArchiveBaseUrl"]
                          ?? "https://archive-api.open-meteo.com/v1/archive";
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

    /// <summary>
    /// Fetches historical daily weather data from the Open-Meteo archive API.
    /// Returns one entry per day with mean temperature, precipitation sum and weather code.
    /// Days with missing data from the API are silently skipped.
    /// </summary>
    public async Task<List<HistoricalWeatherDay>> GetHistoricalWeatherAsync(
        decimal latitude, decimal longitude,
        DateOnly startDate, DateOnly endDate,
        CancellationToken ct)
    {
        var url = string.Format(
            CultureInfo.InvariantCulture,
            "{0}?latitude={1}&longitude={2}&start_date={3}&end_date={4}&daily=temperature_2m_mean,precipitation_sum,weather_code&timezone=Europe%2FBrussels",
            _archiveBaseUrl,
            latitude,
            longitude,
            startDate.ToString("yyyy-MM-dd"),
            endDate.ToString("yyyy-MM-dd"));

        _logger.LogDebug("Calling Open-Meteo Archive: {Url}", url);

        var response = await _http.GetFromJsonAsync<OpenMeteoArchiveResponse>(url, ct);
        var daily = response?.Daily;

        if (daily is null || daily.Time.Count == 0)
            return [];

        var result = new List<HistoricalWeatherDay>(daily.Time.Count);

        for (int i = 0; i < daily.Time.Count; i++)
        {
            if (!DateOnly.TryParseExact(daily.Time[i], "yyyy-MM-dd", out var date))
                continue;

            // Skip days where any critical value is absent
            if (i >= daily.TemperatureMean.Count || !daily.TemperatureMean[i].HasValue) continue;
            if (i >= daily.PrecipitationSum.Count || !daily.PrecipitationSum[i].HasValue) continue;
            if (i >= daily.WeatherCode.Count     || !daily.WeatherCode[i].HasValue)     continue;

            result.Add(new HistoricalWeatherDay(
                date,
                daily.TemperatureMean[i]!.Value,
                daily.PrecipitationSum[i]!.Value,
                daily.WeatherCode[i]!.Value));
        }

        return result;
    }
}
