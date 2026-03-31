using System;

namespace TouristGuideWeb.Models;

public class Location
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AudioUrl { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string QrCodeData { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}