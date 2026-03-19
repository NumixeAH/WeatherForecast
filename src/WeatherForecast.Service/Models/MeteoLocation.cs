namespace WeatherForecast.Service.Models;

public class MeteoLocation
{
    public int Id { get; set; }
    public string Loc { get; set; } = string.Empty;
    public string Ville { get; set; } = string.Empty;
    public string Pays { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
}
