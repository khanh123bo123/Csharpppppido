using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TouristGuideWeb.Models;
using TouristGuideWeb.Services;

namespace TouristGuideWeb.Controllers;

[Authorize(Roles = "Admin,Owner")]
public class LocalizationsController : Controller
{
    private readonly LocalizationApiService _localizationApiService;
    private readonly LocationApiService _locationApiService;

    // Standard list for dropdown
    private readonly Dictionary<string, string> _supportedLanguages = new()
    {
        { "vi-VN", "Tiếng Việt (vi-VN)" },
        { "en-US", "Tiếng Anh (en-US)" },
        { "zh-CN", "Tiếng Trung (zh-CN)" },
        { "ja-JP", "Tiếng Nhật (ja-JP)" },
        { "ko-KR", "Tiếng Hàn (ko-KR)" }
    };

    public LocalizationsController(LocalizationApiService localizationApiService, LocationApiService locationApiService)
    {
        _localizationApiService = localizationApiService;
        _locationApiService = locationApiService;
    }

    // Step 1: Browse Locations to translate
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var locationsRaw = await _locationApiService.GetAllAsync(cancellationToken);
        
        var locations = User.IsInRole("Owner") && !User.IsInRole("Admin")
            ? locationsRaw.Where(l => string.Equals(l.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase)).ToList()
            : locationsRaw.ToList();

        return View(locations);
    }

    // Step 2: Manage Translations for a Location
    public async Task<IActionResult> Manage(int id, CancellationToken cancellationToken)
    {
        var location = await _locationApiService.GetLocationByIdAsync(id, cancellationToken);
        if (location == null) return NotFound();

        if (User.IsInRole("Owner") && !User.IsInRole("Admin") && !string.Equals(location.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var localizations = await _localizationApiService.GetLocalizationsByLocationAsync(id, cancellationToken);
        
        ViewBag.Location = location;
        return View(localizations);
    }

    // Step 3: Create translation
    public async Task<IActionResult> Create(int locationId, CancellationToken cancellationToken)
    {
        var location = await _locationApiService.GetLocationByIdAsync(locationId, cancellationToken);
        if (location == null) return NotFound();

        if (User.IsInRole("Owner") && !User.IsInRole("Admin") && !string.Equals(location.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        ViewBag.Location = location;
        ViewBag.Languages = new SelectList(_supportedLanguages, "Key", "Value");

        return View(new CreateLocalizationRequest { LocationId = locationId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateLocalizationRequest model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Languages = new SelectList(_supportedLanguages, "Key", "Value");
            return View(model);
        }

        var result = await _localizationApiService.CreateOrUpdateAsync(model, cancellationToken);
        if (result != null)
        {
            TempData["SuccessMessage"] = "Đã lưu bản dịch thành công. Audio đang được tạo ngầm.";
            return RedirectToAction(nameof(Manage), new { id = model.LocationId });
        }

        TempData["ErrorMessage"] = "Lưu thất bại.";
        ViewBag.Languages = new SelectList(_supportedLanguages, "Key", "Value");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int locationId, CancellationToken cancellationToken)
    {
        var location = await _locationApiService.GetLocationByIdAsync(locationId, cancellationToken);
        if (location == null) return NotFound();
        if (User.IsInRole("Owner") && !User.IsInRole("Admin") && !string.Equals(location.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var success = await _localizationApiService.DeleteAsync(id, cancellationToken);
        TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "Huỷ bản dịch thành công" : "Lỗi huỷ bản dịch";

        return RedirectToAction(nameof(Manage), new { id = locationId });
    }
}
