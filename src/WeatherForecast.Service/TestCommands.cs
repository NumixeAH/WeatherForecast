using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using WeatherForecast.Service.Services;

namespace WeatherForecast.Service;

public static class TestCommands
{
    public static async Task TestDatabaseAsync(string[] args)
    {
        var config = BuildConfig();
        var connectionString = config.GetConnectionString("MeteoDb");

        Console.WriteLine("Test de connexion à la base de données...");
        Console.WriteLine($"  Serveur: {MaskConnectionString(connectionString)}");

        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  Connexion réussie !");
            Console.ResetColor();

            await using var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM Tbl_MeteoLoc WHERE MeteoLoc_Actif = 1", conn);
            var count = (int)(await cmd.ExecuteScalarAsync())!;
            Console.WriteLine($"  Emplacements actifs: {count}");

            await using var cmd2 = new SqlCommand(
                "SELECT TOP 1 Meteo_Date FROM Tbl_Meteo ORDER BY Meteo_Date DESC", conn);
            var lastDate = await cmd2.ExecuteScalarAsync();
            Console.WriteLine($"  Dernière date météo: {lastDate}");

            await using var cmd3 = new SqlCommand(
                "SELECT COUNT(*) FROM Tbl_MeteoDico", conn);
            var dicoCount = (int)(await cmd3.ExecuteScalarAsync())!;
            Console.WriteLine($"  Entrées dictionnaire météo: {dicoCount}");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  Échec de connexion: {ex.Message}");
            Console.ResetColor();
        }
    }

    public static async Task RunNowAsync(string[] args)
    {
        var config = BuildConfig();

        Console.WriteLine("Exécution immédiate du fetch météo...");
        Console.WriteLine($"  Base: {MaskConnectionString(config.GetConnectionString("MeteoDb"))}");
        Console.WriteLine();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging(b => b.AddConsole());
        services.AddHttpClient<OpenMeteoClient>();
        services.AddTransient<DatabaseService>();

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<DatabaseService>();
        var meteoClient = scope.ServiceProvider.GetRequiredService<OpenMeteoClient>();

        var locations = await db.GetActiveLocationsAsync(CancellationToken.None);
        Console.WriteLine($"Emplacements actifs: {locations.Count}");
        Console.WriteLine();

        foreach (var loc in locations)
        {
            try
            {
                var weather = await meteoClient.GetCurrentWeatherAsync(
                    loc.Latitude, loc.Longitude, CancellationToken.None);

                if (weather is null)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  [{loc.Id}] {loc.Ville,-20} — Pas de données");
                    Console.ResetColor();
                    continue;
                }

                var dicoId = WmoCodeMapper.GetMeteoDicoId(weather.WeatherCode);

                await db.InsertMeteoAsync(loc, weather.Temperature, weather.Precipitation, dicoId, DateOnly.FromDateTime(DateTime.Today), CancellationToken.None);

                await db.WriteLogAsync("R", "Requête",
                    $"INSERT Tbl_Meteo: {loc.Ville} ({loc.Loc}) — {weather.Temperature}°C, precip={weather.Precipitation}, WMO={weather.WeatherCode}→Dico={dicoId}",
                    1, CancellationToken.None);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(
                    $"  [{loc.Id}] {loc.Ville,-20} — {weather.Temperature,5:F1}°C  precip={weather.Precipitation:F1}mm  WMO={weather.WeatherCode}→Dico={dicoId}");
                Console.ResetColor();

                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [{loc.Id}] {loc.Ville,-20} — ERREUR: {ex.Message}");
                Console.ResetColor();
            }
        }

        Console.WriteLine();
        Console.WriteLine("Terminé.");
    }

    private static IConfiguration BuildConfig()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .Build();
    }

    private static string MaskConnectionString(string? cs)
    {
        if (string.IsNullOrEmpty(cs)) return "(vide)";
        var idx = cs.IndexOf("Password=", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return cs;
        var end = cs.IndexOf(';', idx);
        return end > 0
            ? cs[..idx] + "Password=****" + cs[end..]
            : cs[..idx] + "Password=****";
    }
}
