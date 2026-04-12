using System.ComponentModel.DataAnnotations;

namespace TouristGuideWeb.Models;

public class LocationEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Ten khong duoc de trong")]
    [RegularExpression(@"^(?!\s*$).+", ErrorMessage = "Ten khong duoc chi chua khoang trang")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mo ta khong duoc de trong")]
    [RegularExpression(@"^(?!\s*$).+", ErrorMessage = "Mo ta khong duoc chi chua khoang trang")]
    [StringLength(1000, ErrorMessage = "Mo ta khong duoc vuot qua 1000 ky tu")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Latitude khong duoc de trong")]
    public string Latitude { get; set; } = string.Empty;

    [Required(ErrorMessage = "Longitude khong duoc de trong")]
    public string Longitude { get; set; } = string.Empty;

    public string? Category { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "Số điện thoại bắt buộc phải bao gồm đúng 10 số")]
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? ImageUrl { get; set; }
}
