using System.Diagnostics;
using System.Reflection;

namespace WeatherForecast.Service;

public static class ServiceInstaller
{
    private const string ServiceName = "WeatherForecastService";
    private const string DisplayName = "Weather Forecast Service";
    private const string Description = "Récupère automatiquement les données météo via Open-Meteo et les insère dans la base PhM_Meteo.";

    public static void Install()
    {
        var exePath = Environment.ProcessPath
                      ?? Assembly.GetExecutingAssembly().Location;

        Console.WriteLine($"Installation du service '{ServiceName}'...");
        Console.WriteLine($"Exécutable: {exePath}");

        var result = RunSc(
            $"create {ServiceName} binPath=\"{exePath}\" start=delayed-auto DisplayName=\"{DisplayName}\"");

        if (result != 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Échec de l'installation. Vérifiez que vous exécutez en tant qu'administrateur.");
            Console.ResetColor();
            return;
        }

        RunSc($"description {ServiceName} \"{Description}\"");

        RunSc($"failure {ServiceName} reset=86400 actions=restart/60000/restart/60000/restart/60000");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Service '{ServiceName}' installé avec succès.");
        Console.WriteLine("  - Démarrage: Automatique (différé)");
        Console.WriteLine("  - Reprise après crash: Redémarrage après 60s (3 tentatives)");
        Console.WriteLine();
        Console.WriteLine($"Pour démarrer: sc start {ServiceName}");
        Console.ResetColor();
    }

    public static void Uninstall()
    {
        Console.WriteLine($"Arrêt du service '{ServiceName}'...");
        RunSc($"stop {ServiceName}");

        Console.WriteLine($"Suppression du service '{ServiceName}'...");
        var result = RunSc($"delete {ServiceName}");

        if (result == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Service '{ServiceName}' supprimé avec succès.");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Échec de la suppression. Vérifiez que vous exécutez en tant qu'administrateur.");
            Console.ResetColor();
        }
    }

    private static int RunSc(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo("sc.exe", arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(psi);
            if (process is null) return -1;

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(output))
                Console.WriteLine(output.TrimEnd());
            if (!string.IsNullOrWhiteSpace(error))
                Console.Error.WriteLine(error.TrimEnd());

            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Erreur lors de l'exécution de sc.exe: {ex.Message}");
            return -1;
        }
    }
}
