using System.ComponentModel.DataAnnotations;

namespace TouristGuideWeb.Models;

public class LocationCreateViewModel
{
    [Required(ErrorMessage = "Tên không được để trống")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mô tả không được để trống")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mô tả audio không được để trống")]
    public string AudioDescription { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn vị trí trên bản đồ")]
    public string Latitude { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn vị trí trên bản đồ")]
    public string Longitude { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn hoặc nhập danh mục")]
    public string Category { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "Số điện thoại bắt buộc phải bao gồm đúng 10 số")]
    public string? PhoneNumber { get; set; }

    public string? ImageUrl { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập độ ưu tiên")]
    public int Priority { get; set; } = 0;

    public string? QrCodeData { get; set; }
}
