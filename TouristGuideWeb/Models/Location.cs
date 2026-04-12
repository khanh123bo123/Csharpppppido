using System;
using System.ComponentModel.DataAnnotations;

namespace TouristGuideWeb.Models;

public class Location
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Description must be at most 1000 characters.")]
    public string Description { get; set; } = string.Empty;

    public string AudioUrl { get; set; } = string.Empty;

    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
    public double Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
    public double Longitude { get; set; }

    public string QrCodeData { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public string? Category { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? ImageUrl { get; set; }
    
    // Multi-tenant owner tracking
    public string? OwnerEmail { get; set; }
}