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

        var provinceStats = locations
            .GroupBy(InferProvince)
            .Select(group => new ProvinceStatItem
            {
                Province = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Province)
            .ToList();

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
            ProvinceStats = provinceStats,
            NearestToHanoi = nearestToHanoi,
            RecentRatings = recentRatings,
            OnlineDevicesCount = onlineCount
        };

        return View(model);
    }

    private static string InferProvince(Location location)
    {
        // 1. Prioritize Address for actual province
        if (!string.IsNullOrWhiteSpace(location.Address))
        {
            var addr = location.Address.Trim();
            if (addr.Contains(','))
            {
                var tokens = addr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var province = tokens[^1];

                // Standardize common names
                if (province.Contains("Hồ Chí Minh", StringComparison.OrdinalIgnoreCase) || province.Contains("HCM", StringComparison.OrdinalIgnoreCase))
                    return "TP.HCM";
                if (province.Contains("Hà Nội", StringComparison.OrdinalIgnoreCase))
                    return "Hà Nội";

                return province.Replace("Tỉnh", "").Replace("TP.", "").Trim();
            }
        }

        // 2. Latitude/Longitude check (Approximate)
        // Hanoi area
        if (Math.Abs(location.Latitude - HanoiLatitude) <= 0.5 && Math.Abs(location.Longitude - HanoiLongitude) <= 0.5)
        {
            return "Hà Nội";
        }
        // HCM area (Approx)
        if (Math.Abs(location.Latitude - 10.76) <= 0.3 && Math.Abs(location.Longitude - 106.66) <= 0.3)
        {
            return "TP.HCM";
        }

        // 3. Fallback to Category if available (more descriptive than "Khac")
        if (!string.IsNullOrWhiteSpace(location.Category))
        {
            return location.Category;
        }

        // 4. Fallback to Name token if it looks like a city (e.g. "Quan 4 - HCM")
        if (!string.IsNullOrWhiteSpace(location.Name) && location.Name.Contains('-'))
        {
            var tokens = location.Name.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length > 1) return tokens[^1];
        }

        return "Khác";
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
