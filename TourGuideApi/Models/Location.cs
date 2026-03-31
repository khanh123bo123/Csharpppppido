using System;
using System.ComponentModel.DataAnnotations;

namespace TourGuideApi.Models;

public class Location
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public string AudioUrl { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    [Required]
    public string QrCodeData { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
