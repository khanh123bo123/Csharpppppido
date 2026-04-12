using System;

namespace TouristGuideWeb.Models;

public class Tour
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int EstimatedDurationMinutes { get; set; }
    public double EstimatedDistanceKm { get; set; }
    public bool IsActive { get; set; } = true;
    public string? OwnerEmail { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TourLocation
{
    public int Id { get; set; }
    public int TourId { get; set; }
    public int LocationId { get; set; }
    public int OrderIndex { get; set; }
    public Location? Location { get; set; }
}
