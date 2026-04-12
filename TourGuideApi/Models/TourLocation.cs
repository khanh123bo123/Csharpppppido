using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TourGuideApi.Models;

public class TourLocation
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int TourId { get; set; }

    [ForeignKey(nameof(TourId))]
    public Tour Tour { get; set; } = null!;

    [Required]
    public int LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public Location Location { get; set; } = null!;

    public int OrderIndex { get; set; }
}
