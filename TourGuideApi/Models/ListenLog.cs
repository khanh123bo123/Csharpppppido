using System.ComponentModel.DataAnnotations;

namespace TourGuideApi.Models;

public class ListenLog
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int LocationId { get; set; }
    
    [Required]
    public string LanguageCode { get; set; } = string.Empty;
    
    public string? DeviceId { get; set; }
    
    public DateTime ListenedAt { get; set; } = DateTime.UtcNow;
}
