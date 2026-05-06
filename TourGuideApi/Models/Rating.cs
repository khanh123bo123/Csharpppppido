using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TourGuideApi.Models;

public class Rating
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public Location Location { get; set; } = null!;

    [Required]
    [Range(1, 5)]
    public int Stars { get; set; }

    public string? Comment { get; set; }

    public DateTime RatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public string? DeviceIdentifier { get; set; }

    [StringLength(45)]
    public string? UserIp { get; set; }

    [StringLength(100)]
    public string? UserEmail { get; set; }
}
