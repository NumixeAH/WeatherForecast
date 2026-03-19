using WeatherForecast.Service;
using WeatherForecast.Service.Services;

if (args.Length > 0)
{
    switch (args[0].ToLower())
    {
        case "--install":
            ServiceInstaller.Install();
            return;

        case "--uninstall":
            ServiceInstaller.Uninstall();
            return;

        case "--test-db":
            await TestCommands.TestDatabaseAsync(args);
            return;

        case "--run-now":
            await TestCommands.RunNowAsync(args);
            return;

        case "--help":
            PrintHelp();
            return;
    }
}

var builder = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "WeatherForecastService";
    })
    .ConfigureServices((context, services) =>
    {
        services.AddHttpClient<OpenMeteoClient>();
        services.AddTransient<DatabaseService>();
        services.AddHostedService<WeatherWorker>();
    });

var host = builder.Build();
host.Run();

static void PrintHelp()
{
    Console.WriteLine("""
        WeatherForecast Service — Récupération météo automatique via Open-Meteo

        Usage:
          WeatherForecast.Service.exe                  Démarre le service (normal ou Windows Service)
          WeatherForecast.Service.exe --install        Installe le service Windows (admin requis)
          WeatherForecast.Service.exe --uninstall      Désinstalle le service Windows (admin requis)
          WeatherForecast.Service.exe --test-db        Teste la connexion à la base de données
          WeatherForecast.Service.exe --run-now        Exécute un fetch météo immédiatement (mode test)
          WeatherForecast.Service.exe --help           Affiche cette aide
        """);
}
