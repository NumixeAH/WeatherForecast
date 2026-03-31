using WeatherForecast.Service.Services;

namespace WeatherForecast.Service;

public class WeatherWorker : BackgroundService
{
    private readonly ILogger<WeatherWorker> _logger;
    private readonly IServiceProvider _services;
    private readonly int _scheduleHour;
    private readonly int _scheduleMinute;
    private readonly DateOnly? _backfillFromDate;
    private readonly string _diagnosticLogPath;

    public WeatherWorker(ILogger<WeatherWorker> logger, IServiceProvider services, IConfiguration config)
    {
        _logger = logger;
        _services = services;
        _scheduleHour   = config.GetValue("WeatherService:ScheduleHour",   15);
        _scheduleMinute = config.GetValue("WeatherService:ScheduleMinute", 0);

        var raw = config["WeatherService:BackfillFromDate"];
        _backfillFromDate = !string.IsNullOrWhiteSpace(raw)
                            && DateOnly.TryParseExact(raw, "yyyy-MM-dd", out var parsed)
            ? parsed
            : null;

        var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logsDir);
        _diagnosticLogPath = Path.Combine(logsDir, "startup-diagnostics.log");
        TraceDiagnostic(
            $"Worker constructed. BaseDir={AppContext.BaseDirectory}; Schedule={_scheduleHour:D2}:{_scheduleMinute:D2}; BackfillFromDateRaw='{raw ?? "null"}'; Parsed='{_backfillFromDate?.ToString("yyyy-MM-dd") ?? "null"}'");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation(
                "WeatherForecast Service started. Scheduled daily at {Hour:D2}:{Minute:D2}",
                _scheduleHour, _scheduleMinute);
            TraceDiagnostic("ExecuteAsync started.");

            if (_backfillFromDate.HasValue)
            {
                TraceDiagnostic($"Backfill enabled. Starting from {_backfillFromDate.Value:yyyy-MM-dd}.");
                await RunBackfillAsync(_backfillFromDate.Value, stoppingToken);
                TraceDiagnostic("Backfill finished.");
            }
            else
            {
                TraceDiagnostic("Backfill disabled (BackfillFromDate is null/invalid).");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var nextRun = now.Date.AddHours(_scheduleHour).AddMinutes(_scheduleMinute);

                if (nextRun <= now)
                    nextRun = nextRun.AddDays(1);

                var delay = nextRun - now;
                _logger.LogInformation("Next weather fetch scheduled at {NextRun:G} (in {Delay})", nextRun, delay);
                TraceDiagnostic($"Next daily run at {nextRun:yyyy-MM-dd HH:mm:ss} (delay {delay}).");

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    TraceDiagnostic("ExecuteAsync cancellation requested during delay.");
                    break;
                }

                await RunWeatherFetchAsync(stoppingToken);
            }
        }
        catch (Exception ex)
        {
            TraceDiagnostic($"ExecuteAsync fatal exception: {ex}");
            throw;
        }
    }

    private async Task RunWeatherFetchAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting daily weather fetch...");

        using var scope = _services.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<DatabaseService>();
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
            int errorCount   = 0;

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

                    await db.InsertMeteoAsync(loc, weather.Temperature, weather.Precipitation,
                        dicoId, DateOnly.FromDateTime(DateTime.Today), ct);

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

    /// <summary>
    /// Backfills historical weather data from <paramref name="fromDate"/> up to yesterday.
    /// For each location, only missing dates are fetched from the archive API (one batch call
    /// per location). A 1-second delay between locations keeps the free API happy.
    /// Idempotent: re-running the service with the same date will skip already-present data
    /// without making any API calls.
    /// </summary>
    private async Task RunBackfillAsync(DateOnly fromDate, CancellationToken ct)
    {
        var yesterday = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        TraceDiagnostic($"RunBackfillAsync entered. fromDate={fromDate:yyyy-MM-dd}, yesterday={yesterday:yyyy-MM-dd}");

        if (fromDate > yesterday)
        {
            _logger.LogInformation(
                "BackfillFromDate ({Date}) is today or in the future — nothing to backfill.", fromDate);
            TraceDiagnostic($"Backfill skipped: fromDate is in future ({fromDate:yyyy-MM-dd} > {yesterday:yyyy-MM-dd}).");
            return;
        }

        int totalDays = yesterday.DayNumber - fromDate.DayNumber + 1;
        _logger.LogInformation(
            "Backfill requested from {From} to {To} ({Days} days).", fromDate, yesterday, totalDays);
        TraceDiagnostic($"Backfill requested. Range={fromDate:yyyy-MM-dd}..{yesterday:yyyy-MM-dd}, totalDays={totalDays}");

        using var scope = _services.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<DatabaseService>();
        var meteoClient = scope.ServiceProvider.GetRequiredService<OpenMeteoClient>();

        await db.WriteLogAsync(" ", "Démarrage Backfill",
            $"Récupération historique Open-Meteo de {fromDate} à {yesterday} ({totalDays} jours)", 0, ct);

        var locations = await db.GetActiveLocationsAsync(ct);
        TraceDiagnostic($"Backfill loaded {locations.Count} active locations.");

        int totalInserted = 0;
        int totalSkipped  = 0;

        foreach (var loc in locations)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                // Determine which dates are genuinely missing for this location
                var existing     = await db.GetExistingDatesForLocationAsync(loc.Loc, fromDate, yesterday, ct);
                var missingDates = Enumerable
                    .Range(0, totalDays)
                    .Select(i => fromDate.AddDays(i))
                    .Where(d => !existing.Contains(d))
                    .ToList();

                totalSkipped += totalDays - missingDates.Count;

                if (missingDates.Count == 0)
                {
                    _logger.LogDebug("Backfill {Ville}: all dates already present — skipping.", loc.Ville);
                    continue;
                }

                _logger.LogInformation(
                    "Backfill {Ville}: {Missing} missing day(s) out of {Total} (first: {First}, last: {Last})",
                    loc.Ville, missingDates.Count, totalDays, missingDates[0], missingDates[^1]);

                // Single archive API call covers the full range efficiently
                var historical = await meteoClient.GetHistoricalWeatherAsync(
                    loc.Latitude, loc.Longitude, fromDate, yesterday, ct);

                var dataByDate = historical.ToDictionary(h => h.Date);

                int inserted = 0;
                foreach (var date in missingDates)
                {
                    if (!dataByDate.TryGetValue(date, out var day))
                    {
                        _logger.LogWarning(
                            "Backfill {Ville}: no archive data for {Date} — skipped.", loc.Ville, date);
                        continue;
                    }

                    var dicoId = WmoCodeMapper.GetMeteoDicoId(day.WeatherCode);
                    await db.InsertMeteoAsync(loc, day.Temperature, day.Precipitation, dicoId, date, ct);
                    inserted++;
                }

                totalInserted += inserted;

                await db.WriteLogAsync("R", $"Backfill {loc.Ville}",
                    $"{inserted} jours insérés ({fromDate} → {yesterday})", inserted, ct);

                // Respectful pause between locations — keeps the free API happy
                await Task.Delay(1000, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backfill error for {Ville} ({Loc})", loc.Ville, loc.Loc);
                await db.WriteLogAsync("E", $"Erreur backfill: {loc.Ville}",
                    ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message, 0, ct);
            }
        }

        var summary = $"Backfill terminé: {totalInserted} jour(s) insérés, {totalSkipped} déjà présents, {locations.Count} emplacement(s).";
        _logger.LogInformation(summary);
        await db.WriteLogAsync(" ", "Fin Backfill", summary, totalInserted, ct);
        TraceDiagnostic(summary);
    }

    private void TraceDiagnostic(string message)
    {
        try
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | PID={Environment.ProcessId} | {message}{Environment.NewLine}";
            File.AppendAllText(_diagnosticLogPath, line);
        }
        catch
        {
            // Keep diagnostics best-effort only.
        }
    }
}
