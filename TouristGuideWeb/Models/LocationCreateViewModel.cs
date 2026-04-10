using System.ComponentModel.DataAnnotations;

namespace TouristGuideWeb.Models;

public class LocationCreateViewModel
{
    [Required(ErrorMessage = "Ten khong duoc de trong")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mo ta khong duoc de trong")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui long chon vi tri tren ban do")]
    public string Latitude { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui long chon vi tri tren ban do")]
    public string Longitude { get; set; } = string.Empty;

    public string? Category { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? ImageUrl { get; set; }
}
