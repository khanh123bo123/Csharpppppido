using System.ComponentModel.DataAnnotations;

namespace TouristGuideWeb.Models;

public class LocationCreateViewModel
{
    [Required(ErrorMessage = "Ten khong duoc de trong")]
    [RegularExpression(@"^(?!\s*$).+", ErrorMessage = "Ten khong duoc chi chua khoang trang")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mo ta khong duoc de trong")]
    [RegularExpression(@"^(?!\s*$).+", ErrorMessage = "Mo ta khong duoc chi chua khoang trang")]
    [StringLength(1000, ErrorMessage = "Mo ta khong duoc vuot qua 1000 ky tu")]
    public string Description { get; set; } = string.Empty;

    [Range(-90, 90, ErrorMessage = "Latitude phai trong khoang -90 den 90")]
    public double Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Longitude phai trong khoang -180 den 180")]
    public double Longitude { get; set; }
}
