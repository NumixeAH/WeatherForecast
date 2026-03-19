using WeatherForecast.Service.Services;

namespace WeatherForecast.Service;

public class WeatherWorker : BackgroundService
{
    private readonly ILogger<WeatherWorker> _logger;
    private readonly IServiceProvider _services;
    private readonly int _scheduleHour;
    private readonly int _scheduleMinute;

    public WeatherWorker(ILogger<WeatherWorker> logger, IServiceProvider services, IConfiguration config)
    {
        _logger = logger;
        _services = services;
        _scheduleHour = config.GetValue("WeatherService:ScheduleHour", 15);
        _scheduleMinute = config.GetValue("WeatherService:ScheduleMinute", 0);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "WeatherForecast Service started. Scheduled daily at {Hour:D2}:{Minute:D2}",
            _scheduleHour, _scheduleMinute);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextRun = now.Date.AddHours(_scheduleHour).AddMinutes(_scheduleMinute);

            if (nextRun <= now)
                nextRun = nextRun.AddDays(1);

            var delay = nextRun - now;
            _logger.LogInformation("Next weather fetch scheduled at {NextRun:G} (in {Delay})", nextRun, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunWeatherFetchAsync(stoppingToken);
        }
    }

    private async Task RunWeatherFetchAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting daily weather fetch...");

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseService>();
        var meteoClient = scope.ServiceProvider.GetRequiredService<OpenMeteoClient>();

        await db.WriteLogAsync(" ", "Démarrage application",
            "WeatherForecast Service — Récupération automatique de la météo via Open-Meteo API", 0, ct);

        try
        {
            if (await db.HasAlreadyRunTodayAsync(ct))
            {
                _logger.LogWarning("Weather data already exists for today. Skipping.");
                await db.WriteLogAsync(" ", "Vérification",
                    "Données météo déjà présentes pour aujourd'hui — exécution ignorée.", 0, ct);
                return;
            }

            var locations = await db.GetActiveLocationsAsync(ct);
            _logger.LogInformation("Fetched {Count} active locations from database.", locations.Count);

            int successCount = 0;
            int errorCount = 0;

            foreach (var loc in locations)
            {
                try
                {
                    var weather = await meteoClient.GetCurrentWeatherAsync(loc.Latitude, loc.Longitude, ct);

                    if (weather is null)
                    {
                        _logger.LogWarning("No weather data returned for {Ville} ({Loc})", loc.Ville, loc.Loc);
                        errorCount++;
                        continue;
                    }

                    var dicoId = WmoCodeMapper.GetMeteoDicoId(weather.WeatherCode);

                    await db.InsertMeteoAsync(loc, weather.Temperature, weather.Precipitation, dicoId, ct);

                    await db.WriteLogAsync("R", "Requête",
                        $"INSERT Tbl_Meteo: {loc.Ville} ({loc.Loc}) — {weather.Temperature}°C, precip={weather.Precipitation}, WMO={weather.WeatherCode}→Dico={dicoId}",
                        1, ct);

                    successCount++;
                    _logger.LogInformation(
                        "  [{Index}/{Total}] {Ville}: {Temp}°C, precip={Precip}, code={WMO}→dico={Dico}",
                        successCount + errorCount, locations.Count,
                        loc.Ville, weather.Temperature, weather.Precipitation,
                        weather.WeatherCode, dicoId);

                    // Small delay to be polite with the free API
                    await Task.Delay(500, ct);
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _logger.LogError(ex, "Error fetching weather for {Ville} ({Loc})", loc.Ville, loc.Loc);
                    await db.WriteLogAsync("E", $"Erreur: {loc.Ville}",
                        ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message, 0, ct);
                }
            }

            var summary = $"Terminé: {successCount} succès, {errorCount} erreur(s) sur {locations.Count} emplacements.";
            _logger.LogInformation(summary);
            await db.WriteLogAsync(" ", "Fin exécution", summary, successCount, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during weather fetch.");
            await db.WriteLogAsync("E", "Erreur critique",
                ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message, 0, ct);
        }
    }
}
