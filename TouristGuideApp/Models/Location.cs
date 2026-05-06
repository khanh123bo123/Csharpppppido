namespace TouristGuideApp.Models;

public class Location
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AudioUrl { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string QrCodeData { get; set; } = string.Empty;
    public double? Distance { get; set; }
    public string? Category { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? ImageUrl { get; set; }
    public double AverageRating { get; set; } = 0;
    public int RatingCount { get; set; } = 0;
}
