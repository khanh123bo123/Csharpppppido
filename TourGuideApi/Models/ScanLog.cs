using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TourGuideApi.Models;

public class ScanLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public Location Location { get; set; } = null!;

    [Required]
    [StringLength(10)]
    public string LanguageCode { get; set; } = "vi-VN";

    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public string? DeviceIdentifier { get; set; }

    [StringLength(45)]
    public string? UserIp { get; set; }
}
