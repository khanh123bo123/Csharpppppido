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

    // Cho phép để trống lúc tạo, sẽ cập nhật sau
    public string? AudioUrl { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    // Tự động tạo dữ liệu QR nếu chưa có
    public string? QrCodeData { get; set; } = string.Empty;

    // Additional properties for suggestions
    public string? Category { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? ImageUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Field to isolate POIs per owner
    public string? OwnerEmail { get; set; }

    public double AverageRating { get; set; } = 0;
    public int RatingCount { get; set; } = 0;
}
