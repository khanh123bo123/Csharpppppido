using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TouristGuideWeb.Models;
using TouristGuideWeb.Services;

namespace TouristGuideWeb.Controllers;

[Authorize(Roles = "Admin,Owner")]
public class LocalizationsController : Controller
{
    private readonly LocalizationApiService _localizationApiService;
    private readonly LocationApiService _locationApiService;

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

    [HttpGet]
    public async Task<IActionResult> Status(int locationId, CancellationToken cancellationToken)
    {
        var location = await _locationApiService.GetLocationByIdAsync(locationId, cancellationToken);
        if (location == null) return NotFound();

        if (User.IsInRole("Owner") && !User.IsInRole("Admin") && !string.Equals(location.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var localizations = await _localizationApiService.GetLocalizationsByLocationAsync(locationId, cancellationToken);

        return Json(localizations.Select(l => new
        {
            id = l.Id,
            languageCode = l.LanguageCode,
            audioGenerationStatus = l.AudioGenerationStatus,
            audioStreamUrl = Url.Action("Stream", "Audio", new { id = l.Id })
        }));
    }

    // Step 3: Create translation
    public async Task<IActionResult> Create(int locationId, CancellationToken cancellationToken)
    {
        var location = await _locationApiService.GetLocationByIdAsync(locationId, cancellationToken);
        if (location == null) return NotFound();

        if (User.IsInRole("Owner") && !User.IsInRole("Admin") && !string.Equals(location.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        ViewBag.Location = location;
        return View(new CreateLocalizationRequest { LocationId = locationId, LanguageCode = "vi-VN", LocalizedName = location.Name });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateLocalizationRequest model, CancellationToken cancellationToken)
    {
        // Web UX: always enter Vietnamese once, then backend generates the full 5-language pack.
        model.LanguageCode = "vi-VN";

        if (!ModelState.IsValid)
        {
            ViewBag.Location = await _locationApiService.GetLocationByIdAsync(model.LocationId, cancellationToken);
            return View(model);
        }

        var packResponse = await _localizationApiService.GenerateLocalizationPackAsync(
            new GenerateLocalizationPackRequest
            {
                LocationId = model.LocationId,
                VietnameseName = model.LocalizedName,
                VietnameseDescription = model.LocalizedDescription
            },
            cancellationToken);

        if (packResponse != null && string.Equals(packResponse.Status, "queued", StringComparison.OrdinalIgnoreCase))
        {
            TempData["SuccessMessage"] = "Đã lưu tiếng Việt và đang tạo Audio cho 5 ngôn ngữ (vi/en/zh/ja/ko).";
            return RedirectToAction(nameof(Manage), new { id = model.LocationId });
        }

        TempData["ErrorMessage"] = packResponse?.Message ?? "Lưu thất bại (chưa cấu hình dịch tự động hoặc API lỗi).";
        ViewBag.Location = await _locationApiService.GetLocationByIdAsync(model.LocationId, cancellationToken);
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
