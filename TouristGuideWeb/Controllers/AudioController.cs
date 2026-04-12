using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TouristGuideWeb.Models;
using TouristGuideWeb.Services;

namespace TouristGuideWeb.Controllers;

[Authorize(Roles = "Admin,Owner")]
public class AudioController : Controller
{
    private readonly LocalizationApiService _localizationApiService;
    private readonly LocationApiService _locationApiService;
    private readonly IConfiguration _config;

    public AudioController(LocalizationApiService localizationApiService, LocationApiService locationApiService, IConfiguration config)
    {
        _localizationApiService = localizationApiService;
        _locationApiService = locationApiService;
        _config = config;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var locationsRaw = await _locationApiService.GetAllAsync(cancellationToken);
        
        var locations = User.IsInRole("Owner") && !User.IsInRole("Admin")
            ? locationsRaw.Where(l => string.Equals(l.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase)).ToList()
            : locationsRaw.ToList();

        // N+1 Fetching for all localizations. Acceptable for small admin panels.
        var allAudios = new List<LocalizationDto>();
        foreach(var loc in locations)
        {
            var locals = await _localizationApiService.GetLocalizationsByLocationAsync(loc.Id, cancellationToken);
            foreach(var l in locals)
            {
                l.Location = loc; // Attach location for UI rendering
                allAudios.Add(l);
            }
        }

        ViewBag.ApiBaseUrl = _config["ApiSettings:BaseUrl"];
        return View(allAudios.OrderByDescending(a => a.Id).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegenerateAudio(int id, CancellationToken cancellationToken)
    {
        var result = await _localizationApiService.GenerateAudioAsync(id, cancellationToken);
        if (result != null)
        {
            TempData["SuccessMessage"] = "Đã gửi lệnh tạo lại Audio thành công. Trạng thái: " + result.Status;
        }
        else
        {
            TempData["ErrorMessage"] = "Không thể kết nối đến máy chủ AI để tạo giọng đọc.";
        }
        return RedirectToAction(nameof(Index));
    }
}
