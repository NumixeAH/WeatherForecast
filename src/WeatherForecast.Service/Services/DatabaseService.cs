using Microsoft.Data.SqlClient;
using WeatherForecast.Service.Models;

namespace WeatherForecast.Service.Services;

public class DatabaseService
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(IConfiguration config, ILogger<DatabaseService> logger)
    {
        _connectionString = config.GetConnectionString("MeteoDb")
            ?? throw new InvalidOperationException("Missing MeteoDb connection string.");
        _logger = logger;
    }

    public async Task<List<MeteoLocation>> GetActiveLocationsAsync(CancellationToken ct)
    {
        var locations = new List<MeteoLocation>();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(
            """
            SELECT MeteoLoc_Id, MeteoLoc_Loc, MeteoLoc_Ville, MeteoLoc_Pays,
                   MeteoLoc_Latitude, MeteoLoc_Longitude
            FROM Tbl_MeteoLoc
            WHERE MeteoLoc_Actif = 1
            ORDER BY MeteoLoc_Id
            """, conn);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            locations.Add(new MeteoLocation
            {
                Id = reader.GetInt32(0),
                Loc = reader.GetString(1),
                Ville = reader.GetString(2),
                Pays = reader.GetString(3),
                Latitude = reader.GetDecimal(4),
                Longitude = reader.GetDecimal(5),
            });
        }

        return locations;
    }

    public async Task<bool> HasAlreadyRunTodayAsync(CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(
            """
            SELECT COUNT(1) FROM Tbl_Meteo
            WHERE CAST(Meteo_Date AS DATE) = CAST(GETDATE() AS DATE)
            """, conn);

        var count = (int)(await cmd.ExecuteScalarAsync(ct))!;
        return count > 0;
    }

    public async Task InsertMeteoAsync(
        MeteoLocation location, decimal temperature, decimal precipitation,
        int meteoDicoId, CancellationToken ct)
    {
        var now = DateTime.Now;
        var today = now.Date;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(
            """
            INSERT INTO Tbl_Meteo
                (Meteo_Date, Meteo_Temperature, Meteo_Pays, Meteo_Precipitation,
                 Meteo_Loc, Meteo_Ville, Meteo_MeteoDico_Id, Meteo_StampCrea, Meteo_StampModi)
            VALUES
                (@date, @temp, @pays, @precip, @loc, @ville, @dicoId, @stampCrea, @stampModi)
            """, conn);

        cmd.Parameters.AddWithValue("@date", today);
        cmd.Parameters.AddWithValue("@temp", temperature);
        cmd.Parameters.AddWithValue("@pays", location.Pays);
        cmd.Parameters.AddWithValue("@precip", precipitation);
        cmd.Parameters.AddWithValue("@loc", location.Loc);
        cmd.Parameters.AddWithValue("@ville", location.Ville);
        cmd.Parameters.AddWithValue("@dicoId", meteoDicoId);
        cmd.Parameters.AddWithValue("@stampCrea", now);
        cmd.Parameters.AddWithValue("@stampModi", now);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task WriteLogAsync(
        string type, string fonction, string message, int nbreRecord, CancellationToken ct)
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = new SqlCommand(
                """
                INSERT INTO Tbl_Log (Log_Date, Log_User_Id, Log_Type, Log_Fonction, Log_Msg, Log_NbreRecord)
                VALUES (@date, @user, @type, @fonction, @msg, @nbre)
                """, conn);

            cmd.Parameters.AddWithValue("@date", DateTime.Now);
            cmd.Parameters.AddWithValue("@user", "WeatherService");
            cmd.Parameters.AddWithValue("@type", type);
            cmd.Parameters.AddWithValue("@fonction", fonction);
            cmd.Parameters.AddWithValue("@msg", message);
            cmd.Parameters.AddWithValue("@nbre", nbreRecord);

            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write to Tbl_Log");
        }
    }
}
