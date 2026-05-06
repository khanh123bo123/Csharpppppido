using System.Collections.Generic;

namespace TouristGuideWeb.Models;

public class DashboardViewModel
{
    public int TotalLocations { get; set; }
    public int TotalTours { get; set; }
    public int TotalAudios { get; set; }
    public int TotalUsers { get; set; }
    public List<ProvinceStatItem> ProvinceStats { get; set; } = new();
    public List<NearestLocationItem> NearestToHanoi { get; set; } = new();
    public List<RatingDto> RecentRatings { get; set; } = new();
    public int OnlineDevicesCount { get; set; }
}

public class ProvinceStatItem
{
    public string Province { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class NearestLocationItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double DistanceKm { get; set; }
}