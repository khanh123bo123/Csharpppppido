using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TourGuideApi.Models;

public class AudioPlayLog
{
    [Key]
    public int Id { get; set; }

    public int LocalizationId { get; set; }

    [ForeignKey(nameof(LocalizationId))]
    public Localization Localization { get; set; } = null!;

    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;
}
