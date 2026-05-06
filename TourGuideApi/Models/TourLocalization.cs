using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TourGuideApi.Models;

public class TourLocalization
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int TourId { get; set; }

    [ForeignKey("TourId")]
    public Tour? Tour { get; set; }

    [Required]
    [StringLength(10)]
    public string LanguageCode { get; set; } = "en-US";

    [Required]
    [StringLength(200)]
    public string LocalizedName { get; set; } = string.Empty;

    public string LocalizedDescription { get; set; } = string.Empty;
}
