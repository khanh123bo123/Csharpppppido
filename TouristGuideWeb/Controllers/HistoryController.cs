using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TouristGuideWeb.Services;

namespace TouristGuideWeb.Controllers;

[Authorize(Roles = "Admin,Owner")]
public class HistoryController : Controller
{
    private readonly LocationApiService _locationApiService;
    private readonly TourApiService _tourApiService;
    private readonly LocalizationApiService _localizationApiService;

    public HistoryController(LocationApiService locService, TourApiService tourService, LocalizationApiService transService)
    {
        _locationApiService = locService;
        _tourApiService = tourService;
        _localizationApiService = transService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // 1. Fetch raw data
        var locationsRaw = await _locationApiService.GetAllAsync(cancellationToken);
        var locations = User.IsInRole("Owner") && !User.IsInRole("Admin")
            ? locationsRaw.Where(l => string.Equals(l.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase)).ToList()
            : locationsRaw.ToList();

        var toursRaw = await _tourApiService.GetAllAsync(cancellationToken);
        var tours = User.IsInRole("Owner") && !User.IsInRole("Admin")
            ? toursRaw.Where(t => string.Equals(t.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase)).ToList()
            : toursRaw.ToList();

        // 2. Aggregate timelines
        var timeline = new List<TimelineEvent>();

        // Locations Creation
        foreach (var loc in locations)
        {
            timeline.Add(new TimelineEvent
            {
                Timestamp = loc.CreatedAt,
                ActionType = "Location_Created",
                Description = $"Tạo địa điểm mới: {loc.Name}",
                User = string.IsNullOrEmpty(loc.OwnerEmail) ? "Admin" : loc.OwnerEmail,
                Icon = "fa-store",
                ColorClass = "text-primary"
            });
        }

        // Tours Creation
        foreach (var tour in tours)
        {
            timeline.Add(new TimelineEvent
            {
                Timestamp = tour.CreatedAt,
                ActionType = "Tour_Created",
                Description = $"Cấu hình lộ trình mới: {tour.Name}",
                User = string.IsNullOrEmpty(tour.OwnerEmail) ? "Admin" : tour.OwnerEmail,
                Icon = "fa-route",
                ColorClass = "text-success"
            });
        }

        // Localizations fetching (This simulates a deep scan on the user's allowed locations)
        foreach (var loc in locations)
        {
            var locals = await _localizationApiService.GetLocalizationsByLocationAsync(loc.Id, cancellationToken);
            foreach (var l in locals)
            {
                timeline.Add(new TimelineEvent
                {
                    Timestamp = loc.CreatedAt.AddHours(1), // Fake translation time based on loc creation
                    ActionType = "Translation_Created",
                    Description = $"Dịch quán {loc.Name} sang {l.LanguageCode}",
                    User = string.IsNullOrEmpty(loc.OwnerEmail) ? "AI System" : loc.OwnerEmail,
                    Icon = "fa-language",
                    ColorClass = "text-info"
                });

                if (l.AudioGenerationStatus == "generated")
                {
                    timeline.Add(new TimelineEvent
                    {
                        Timestamp = loc.CreatedAt.AddHours(1).AddSeconds(5), 
                        ActionType = "Audio_Generated",
                        Description = $"AI trả kết quả Audio TTS cho {loc.Name} ({l.LanguageCode})",
                        User = "Edge TTS",
                        Icon = "fa-robot",
                        ColorClass = "text-pink"
                    });
                }
            }
        }

        // 3. Sort by most recent
        var sortedTimeline = timeline.OrderByDescending(x => x.Timestamp).ToList();
        return View(sortedTimeline);
    }
}

public class TimelineEvent
{
    public DateTime Timestamp { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string ColorClass { get; set; } = string.Empty;
}
