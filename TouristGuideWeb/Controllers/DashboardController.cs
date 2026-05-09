using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TouristGuideWeb.Models;
using TouristGuideWeb.Services;

namespace TouristGuideWeb.Controllers;

[Authorize(Roles = "Admin,Owner")]
public class DashboardController : Controller
{
    private const double HanoiLatitude = 21.027763;
    private const double HanoiLongitude = 105.83416;
    private readonly LocationApiService _locationApiService;
    private readonly TourApiService _tourApiService;
    private readonly LocalizationApiService _localizationApiService;
    private readonly AuthApiService _authApiService;

    public DashboardController(LocationApiService locationApiService, TourApiService tourApiService, LocalizationApiService localizationApiService, AuthApiService authApiService)
    {
        _locationApiService = locationApiService;
        _tourApiService = tourApiService;
        _localizationApiService = localizationApiService;
        _authApiService = authApiService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var locationsRaw = await _locationApiService.GetAllAsync(cancellationToken);
        
        var locations = User.IsInRole("Owner") && !User.IsInRole("Admin")
            ? locationsRaw.Where(l => string.Equals(l.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase)).ToList()
            : locationsRaw.ToList();

        var toursRaw = await _tourApiService.GetAllAsync(cancellationToken);
        var tours = User.IsInRole("Owner") && !User.IsInRole("Admin")
            ? toursRaw.Where(t => string.Equals(t.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase)).ToList()
            : toursRaw.ToList();

        var totalListens = await _locationApiService.GetListenStatsAsync(cancellationToken);

        var authStats = await _authApiService.GetStatsAsync(cancellationToken);
        var totalUsers = authStats?.TotalUsers ?? 0;


        var nearestToHanoi = locations
            .Select(location => new NearestLocationItem
            {
                Id = location.Id,
                Name = location.Name,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                DistanceKm = Math.Round(
                    CalculateHaversineDistanceKm(HanoiLatitude, HanoiLongitude, location.Latitude, location.Longitude),
                    2)
            })
            .OrderBy(item => item.DistanceKm)
            .Take(5)
            .ToList();

        var recentRatingsRaw = await _locationApiService.GetRecentRatingsAsync(cancellationToken);
        var recentRatings = User.IsInRole("Owner") && !User.IsInRole("Admin")
            ? recentRatingsRaw.Where(r => locations.Any(l => l.Id == r.LocationId)).ToList()
            : recentRatingsRaw.ToList();

        var onlineCount = await _locationApiService.GetOnlineCountAsync(cancellationToken);
        
        var model = new DashboardViewModel
        {
            TotalLocations = locations.Count,
            TotalTours = tours.Count,
            TotalAudios = totalListens,
            TotalUsers = totalUsers,
            ProvinceStats = new(),
            NearestToHanoi = nearestToHanoi,
            RecentRatings = recentRatings,
            OnlineDevicesCount = onlineCount
        };

        return View(model);
    }


    private static double CalculateHaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371;

        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2))
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }

    [HttpGet]
    public async Task<IActionResult> GetOnlineCount(CancellationToken cancellationToken)
    {
        var count = await _locationApiService.GetOnlineCountAsync(cancellationToken);
        return Json(new { onlineCount = count });
    }

    [HttpGet]
    public async Task<IActionResult> GetListenStats(CancellationToken cancellationToken)
    {
        var count = await _locationApiService.GetListenStatsAsync(cancellationToken);
        return Json(new { totalListens = count });
    }
}
