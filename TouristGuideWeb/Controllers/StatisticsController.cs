using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TouristGuideWeb.Services;

namespace TouristGuideWeb.Controllers;

[Authorize(Roles = "Admin,Owner")]
public class StatisticsController : Controller
{
    private readonly LocationApiService _locationApiService;
    private readonly TourApiService _tourApiService;
    private readonly LocalizationApiService _localizationApiService;

    public StatisticsController(LocationApiService loc, TourApiService tour, LocalizationApiService trans)
    {
        _locationApiService = loc;
        _tourApiService = tour;
        _localizationApiService = trans;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var locationsRaw = await _locationApiService.GetAllAsync(cancellationToken);
        var locations = User.IsInRole("Owner") && !User.IsInRole("Admin")
            ? locationsRaw.Where(l => string.Equals(l.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase)).ToList()
            : locationsRaw.ToList();

        var categories = locations.GroupBy(x => x.Category)
                                  .Select(g => new { Label = g.Key, Count = g.Count() })
                                  .ToList();

        var languages = new Dictionary<string, int>();
        var audioStatus = new Dictionary<string, int> { { "Hoàn tất/Cached", 0 }, { "Đang tạo/Pending", 0 }, { "Lỗi/Failed", 0 } };

        foreach (var loc in locations)
        {
            var locals = await _localizationApiService.GetLocalizationsByLocationAsync(loc.Id, cancellationToken);
            foreach (var l in locals)
            {
                if (languages.ContainsKey(l.LanguageCode)) languages[l.LanguageCode]++;
                else languages[l.LanguageCode] = 1;

                if (l.AudioGenerationStatus == "generated" || l.AudioGenerationStatus == "cached")
                    audioStatus["Hoàn tất/Cached"]++;
                else if (l.AudioGenerationStatus == "pending")
                    audioStatus["Đang tạo/Pending"]++;
                else
                    audioStatus["Lỗi/Failed"]++;
            }
        }

        ViewBag.CategoryLabels = categories.Select(x => x.Label).ToArray();
        ViewBag.CategoryData = categories.Select(x => x.Count).ToArray();

        ViewBag.LangLabels = languages.Keys.ToArray();
        ViewBag.LangData = languages.Values.ToArray();

        ViewBag.AudioLabels = audioStatus.Keys.ToArray();
        ViewBag.AudioData = audioStatus.Values.ToArray();

        return View();
    }
}
