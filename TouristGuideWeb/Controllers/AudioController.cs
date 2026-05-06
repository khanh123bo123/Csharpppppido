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
    private readonly TtsSettingsApiService _ttsSettingsApiService;

    public AudioController(
        LocalizationApiService localizationApiService,
        LocationApiService locationApiService,
        TtsSettingsApiService ttsSettingsApiService)
    {
        _localizationApiService = localizationApiService;
        _locationApiService = locationApiService;
        _ttsSettingsApiService = ttsSettingsApiService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var edgeTts = await _ttsSettingsApiService.GetEdgeTtsSettingsAsync(cancellationToken);
        ViewBag.EdgeTtsSpeechRate = edgeTts?.SpeechRate;
        ViewBag.EdgeTtsRate = edgeTts?.Rate;

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

        return View(allAudios.OrderByDescending(a => a.Id).ToList());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSpeechRate(double speechRate, CancellationToken cancellationToken)
    {
        // Keep consistent with API clamping.
        if (double.IsNaN(speechRate) || double.IsInfinity(speechRate) || speechRate <= 0)
        {
            TempData["ErrorMessage"] = "Tốc độ đọc không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var updated = await _ttsSettingsApiService.UpdateSpeechRateAsync(speechRate, cancellationToken);
        if (updated != null)
        {
            TempData["SuccessMessage"] = $"Đã cập nhật tốc độ giọng đọc Edge-TTS: {updated.SpeechRate:0.##}x";
        }
        else
        {
            TempData["ErrorMessage"] = "Không thể cập nhật tốc độ đọc. Hãy kiểm tra TourGuideApi đang chạy.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Stream(int id, CancellationToken cancellationToken)
    {
        var audioBytes = await _localizationApiService.GetAudioBytesAsync(id, cancellationToken);
        if (audioBytes is null || audioBytes.Length == 0)
        {
            return NotFound();
        }

        return File(audioBytes, "audio/mpeg", enableRangeProcessing: false);
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAudio(int id, CancellationToken cancellationToken)
    {
        var ok = await _localizationApiService.DeleteAudioAsync(id, cancellationToken);
        if (ok)
        {
            TempData["SuccessMessage"] = "Đã xoá audio cache (giải phóng dung lượng).";
        }
        else
        {
            TempData["ErrorMessage"] = "Không thể xoá audio cache.";
        }

        return RedirectToAction(nameof(Index));
    }
}
