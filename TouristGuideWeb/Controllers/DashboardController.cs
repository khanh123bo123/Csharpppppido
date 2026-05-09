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

    public DashboardController(LocationApiService locationApiService, TourApiService tourApiService, LocalizationApiService localizationApiService)
    {
        _locationApiService = locationApiService;
        _tourApiService = tourApiService;
        _localizationApiService = localizationApiService;
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

        var localTasks = locations.Select(async loc =>
        {
            var locals = await _localizationApiService.GetLocalizationsByLocationAsync(loc.Id, cancellationToken);
            return locals.Sum(l => l.PlayCount);
        });

        var audioCounts = await Task.WhenAll(localTasks);
        var totalAudios = audioCounts.Sum();

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

        var model = new DashboardViewModel
        {
            TotalLocations = locations.Count,
            TotalTours = tours.Count,
            TotalAudios = totalAudios,
            ProvinceStats = provinceStats,
            NearestToHanoi = nearestToHanoi
        };

        return View(model);
    }

    private static string InferProvince(Location location)
    {
        if (!string.IsNullOrWhiteSpace(location.Name))
        {
            var name = location.Name.Trim();

            if (name.Contains(','))
            {
                var tokens = name.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (tokens.Length > 1)
                {
                    return tokens[^1];
                }
            }

            if (name.Contains('-'))
            {
                var tokens = name.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (tokens.Length > 1)
                {
                    return tokens[0];
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(location.Description))
        {
            var description = location.Description.Trim();
            if (description.Contains(','))
            {
                var tokens = description.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return tokens[^1];
            }
        }

        if (Math.Abs(location.Latitude - HanoiLatitude) <= 0.8 && Math.Abs(location.Longitude - HanoiLongitude) <= 0.8)
        {
            return "Ha Noi";
        }

        return "Khac";
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
}