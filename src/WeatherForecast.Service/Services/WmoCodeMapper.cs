namespace WeatherForecast.Service.Services;

/// <summary>
/// Maps WMO weather interpretation codes (from Open-Meteo API)
/// to existing MeteoDico_Id values in Tbl_MeteoDico.
///
/// MeteoDico IDs 16-28 are the "active" set used by the legacy program.
/// Default fallback = 17 (Très nuageux / Overcast).
/// </summary>
public static class WmoCodeMapper
{
    private static readonly Dictionary<int, int> WmoToMeteoDico = new()
    {
        // Clear sky / Mainly clear → 19 (Temps clair)
        { 0, 19 },
        { 1, 19 },

        // Partly cloudy → 16 (Peu nuageux)
        { 2, 16 },

        // Overcast → 17 (Très nuageux)
        { 3, 17 },

        // Fog → 17 (Très nuageux) — closest match in legacy table
        { 45, 17 },

        // Depositing rime fog → 28 (Brouillard givrant)
        { 48, 28 },

        // Drizzle (light/moderate/dense) → 22 (Pluie légère)
        { 51, 22 },
        { 53, 22 },
        { 55, 22 },

        // Freezing drizzle → 22 (Pluie légère)
        { 56, 22 },
        { 57, 22 },

        // Rain slight → 22 (Pluie légère)
        { 61, 22 },

        // Rain moderate → 18 (Pluie)
        { 63, 18 },

        // Rain heavy → 18 (Pluie)
        { 65, 18 },

        // Freezing rain → 18 (Pluie)
        { 66, 18 },
        { 67, 18 },

        // Snow fall slight → 26 (Neige légère)
        { 71, 26 },

        // Snow fall moderate → 27 (Chute de neige)
        { 73, 27 },

        // Snow fall heavy → 27 (Chute de neige)
        { 75, 27 },

        // Snow grains → 26 (Neige légère)
        { 77, 26 },

        // Rain showers slight → 21 (Pluie probable)
        { 80, 21 },

        // Rain showers moderate/violent → 18 (Pluie)
        { 81, 18 },
        { 82, 18 },

        // Snow showers slight → 26 (Neige légère)
        { 85, 26 },

        // Snow showers heavy → 27 (Chute de neige)
        { 86, 27 },

        // Thunderstorm → 23 (Pluie, tempête)
        { 95, 23 },

        // Thunderstorm with hail → 24 (Tempête)
        { 96, 24 },
        { 99, 24 },
    };

    public static int GetMeteoDicoId(int wmoCode)
    {
        return WmoToMeteoDico.TryGetValue(wmoCode, out var dicoId) ? dicoId : 17;
    }
}
