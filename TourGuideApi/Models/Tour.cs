using System;
using System.ComponentModel.DataAnnotations;

namespace TourGuideApi.Models;

public class Tour
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int EstimatedDurationMinutes { get; set; }
    
    public double EstimatedDistanceKm { get; set; }

    public bool IsActive { get; set; } = true;

    // Optional: Only Admin or specific Owner can manage
    public string? OwnerEmail { get; set; } 

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
