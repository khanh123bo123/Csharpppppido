using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourGuideApi.Data;
using TourGuideApi.Models;
using TourGuideApi.Services;
using Microsoft.Extensions.Caching.Memory;

namespace TourGuideApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LocationsController : ControllerBase
{
    private const double EarthRadiusMeters = 6371000;
    private readonly AppDbContext _context;
    private readonly ITextToSpeechService _textToSpeechService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LocationsController> _logger;
    private readonly IOnlineTracker _onlineTracker;

    // Single token source shared across all cache entries so we can invalidate them all at once
    private static CancellationTokenSource _cacheResetToken = new();

    public LocationsController(AppDbContext context, ITextToSpeechService textToSpeechService, IServiceScopeFactory scopeFactory, IMemoryCache cache, ILogger<LocationsController> logger, IOnlineTracker onlineTracker)
    {
        _context = context;
        _textToSpeechService = textToSpeechService;
        _scopeFactory = scopeFactory;
        _cache = cache;
        _logger = logger;
        _onlineTracker = onlineTracker;
    }

    /// <summary>Evicts ALL cached location lists so the next GET returns fresh data from DB.</summary>
    private static void InvalidateLocationsCache()
    {
        var oldToken = Interlocked.Exchange(ref _cacheResetToken, new CancellationTokenSource());
        oldToken.Cancel();
        oldToken.Dispose();
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Location>>> GetLocations([FromQuery] string? query, [FromQuery] string? category)
    {
        string cacheKey = $"locations_{query}_{category}";
        if (_cache.TryGetValue<List<Location>>(cacheKey, out var cachedLocations))
        {
            return cachedLocations!;
        }

        var locationsQuery = _context.Locations.AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            locationsQuery = locationsQuery.Where(l => l.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            query = query.ToLower();
            locationsQuery = locationsQuery.Where(l => 
                (l.Name != null && l.Name.ToLower().Contains(query)) || 
                (l.Address != null && l.Address.ToLower().Contains(query)) ||
                (l.Category != null && l.Category.ToLower().Contains(query)));
        }

        var result = await locationsQuery.ToListAsync();

        // Cache with a linked cancellation token so InvalidateLocationsCache() evicts all variants
        var options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(2))
            .AddExpirationToken(new Microsoft.Extensions.Primitives.CancellationChangeToken(_cacheResetToken.Token));
        _cache.Set(cacheKey, result, options);

        return result;
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<string>>> GetCategories()
    {
        var categories = await _context.Locations
            .Where(l => !string.IsNullOrEmpty(l.Category))
            .Select(l => l.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        return categories;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Location>> GetLocation(int id)
    {
        var location = await _context.Locations.FindAsync(id);

        if (location is null)
        {
            return NotFound();
        }

        return location;
    }

    [HttpGet("by-qr")]
    public async Task<ActionResult<Location>> GetLocationByQr(
        [FromQuery] string code, 
        [FromQuery] string? deviceId = null, 
        [FromQuery] string? lang = "vi-VN")
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest("Query parameter 'code' is required.");
        }

        var location = await _context.Locations
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.QrCodeData == code);

        if (location is null)
        {
            return NotFound();
        }

        // MONITORING & DUPLICATE HANDLING (Mục 2 & 3)
        var userIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var now = DateTime.UtcNow;
        var tenMinutesAgo = now.AddMinutes(-10);

        // Kiểm tra xem có yêu cầu quét trùng lặp từ cùng 1 thiết bị/IP cho cùng 1 địa điểm trong 10 phút qua không
        var isDuplicate = await _context.ScanLogs.AnyAsync(s => 
            s.LocationId == location.Id && 
            (s.DeviceIdentifier == deviceId || s.UserIp == userIp) &&
            s.ScannedAt > tenMinutesAgo);

        if (!isDuplicate)
        {
            _context.ScanLogs.Add(new ScanLog
            {
                LocationId = location.Id,
                LanguageCode = lang ?? "vi-VN",
                ScannedAt = now,
                DeviceIdentifier = deviceId,
                UserIp = userIp
            });
            await _context.SaveChangesAsync();
        }

        if (!string.IsNullOrEmpty(deviceId))
        {
            _onlineTracker.MarkAsOnline(deviceId);
        }

        return location;
    }

    [HttpGet("nearby")]
    public async Task<ActionResult<IEnumerable<Location>>> GetNearbyLocations(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] double radius)
    {
        if (radius <= 0)
        {
            return BadRequest("Query parameter 'radius' must be greater than 0.");
        }

        var locations = await _context.Locations
            .AsNoTracking()
            .ToListAsync();

        var nearbyLocations = locations
            .Select(location => new
            {
                Location = location,
                Distance = CalculateHaversineDistanceMeters(lat, lng, location.Latitude, location.Longitude)
            })
            .Where(x => x.Distance <= radius)
            .OrderBy(x => x.Distance)
            .Select(x => x.Location)
            .ToList();

        return nearbyLocations;
    }

    [HttpPost]
    public async Task<ActionResult<Location>> CreateLocation(Location location)
    {
        location.Id = 0;
        location.QrCodeData = $"LOC_{Guid.NewGuid():N}";

        if (location.CreatedAt == default)
        {
            location.CreatedAt = DateTime.UtcNow;
        }

        _context.Locations.Add(location);
        await _context.SaveChangesAsync();
        InvalidateLocationsCache();

        if (!string.IsNullOrWhiteSpace(location.Description))
        {
            var locationId = location.Id;
            var locName = location.Name;
            var locDesc = location.Description;

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var ttsService = scope.ServiceProvider.GetRequiredService<ITextToSpeechService>();
                    var generator = scope.ServiceProvider.GetRequiredService<LocalizationPackGenerator>();

                    // 1. Generate audio
                    try
                    {
                        var audioUrl = await ttsService.SynthesizeAsync(locDesc, $"location_{locationId}.mp3");
                        var locToUpdate = await dbContext.Locations.FindAsync(locationId);
                        if (locToUpdate != null)
                        {
                            locToUpdate.AudioUrl = audioUrl;
                            await dbContext.SaveChangesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Background TTS generation failed for Location {locationId}: {ex.Message}");
                    }

                    // 2. Generate languages
                    await generator.GeneratePackAsync(locationId, locName, locDesc);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Background pack generation failed for Location {locationId}: {ex.Message}");
                }
            });
        }

        return CreatedAtAction(nameof(GetLocation), new { id = location.Id }, location);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateLocation(int id, Location location)
    {
        if (id != location.Id)
        {
            return BadRequest();
        }

        var existingLocation = await _context.Locations.FindAsync(id);
        if (existingLocation is null)
        {
            return NotFound();
        }

        existingLocation.Name = location.Name;
        existingLocation.Description = location.Description;
        existingLocation.AudioUrl = location.AudioUrl;
        existingLocation.Latitude = location.Latitude;
        existingLocation.Longitude = location.Longitude;
        existingLocation.QrCodeData = location.QrCodeData;
        existingLocation.CreatedAt = location.CreatedAt;
        existingLocation.Category = location.Category;
        existingLocation.PhoneNumber = location.PhoneNumber;
        existingLocation.Address = location.Address;
        existingLocation.ImageUrl = location.ImageUrl;
        existingLocation.OwnerEmail = location.OwnerEmail;

        await _context.SaveChangesAsync();
        InvalidateLocationsCache();

        // AUTO-REGENERATE TRANSLATIONS IF TEXT CHANGED
        var locationId = existingLocation.Id;
        var locName = existingLocation.Name;
        var locDesc = existingLocation.Description;
        
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var generator = scope.ServiceProvider.GetRequiredService<LocalizationPackGenerator>();
                await generator.GeneratePackAsync(locationId, locName, locDesc);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Background pack re-generation failed for Location {locationId}: {ex.Message}");
            }
        });

        return NoContent();
    }

    [HttpPost("reprocess-all")]
    public async Task<IActionResult> ReprocessAll(CancellationToken cancellationToken)
    {
        var locations = await _context.Locations.ToListAsync(cancellationToken);
        foreach (var location in locations)
        {
            var locationId = location.Id;
            var locName = location.Name;
            var locDesc = location.Description;
            
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var generator = scope.ServiceProvider.GetRequiredService<LocalizationPackGenerator>();
                    await generator.GeneratePackAsync(locationId, locName, locDesc);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Background pack generation failed for Location {locationId}: {ex.Message}");
                }
            });
        }
        
        return Ok(new { message = $"Triggered reprocessing for {locations.Count} locations." });
    }

    [HttpGet("scan-logs")]
    public async Task<ActionResult<IEnumerable<object>>> GetScanLogs()
    {
        var logs = await _context.ScanLogs
            .Include(s => s.Location)
            .OrderByDescending(s => s.ScannedAt)
            .Take(100)
            .Select(s => new {
                s.Id,
                s.LocationId,
                LocationName = s.Location.Name,
                s.LanguageCode,
                s.ScannedAt,
                s.DeviceIdentifier,
                s.UserIp
            })
            .ToListAsync();

        return Ok(logs);
    }

    [HttpPost("{id}/rate")]
    public async Task<IActionResult> RateLocation(int id, [FromQuery] int stars, [FromQuery] string? deviceId = null, [FromQuery] string? userEmail = null, CancellationToken cancellationToken = default)
    {
        if (stars < 1 || stars > 5)
        {
            return BadRequest("Rating must be between 1 and 5 stars.");
        }

        var location = await _context.Locations.FindAsync(new object[] { id }, cancellationToken);
        if (location == null)
        {
            return NotFound();
        }

        // 1. Save detailed rating
        var rating = new Rating
        {
            LocationId = id,
            Stars = stars,
            RatedAt = DateTime.UtcNow,
            DeviceIdentifier = deviceId,
            UserEmail = userEmail,
            UserIp = HttpContext.Connection.RemoteIpAddress?.ToString()
        };
        _context.Ratings.Add(rating);

        // 2. Update aggregate average
        double totalScore = (location.AverageRating * location.RatingCount) + stars;
        location.RatingCount++;
        location.AverageRating = Math.Round(totalScore / location.RatingCount, 1);

        await _context.SaveChangesAsync(cancellationToken);
        InvalidateLocationsCache();

        if (!string.IsNullOrEmpty(deviceId))
        {
            _onlineTracker.MarkAsOnline(deviceId);
        }

        return Ok(new { 
            id = location.Id, 
            averageRating = location.AverageRating, 
            ratingCount = location.RatingCount 
        });
    }

    [HttpGet("recent-ratings")]
    public async Task<ActionResult<IEnumerable<object>>> GetRecentRatings()
    {
        var ratings = await _context.Ratings
            .Include(r => r.Location)
            .OrderByDescending(r => r.RatedAt)
            .Take(50)
            .Select(r => new {
                r.Id,
                r.LocationId,
                LocationName = r.Location.Name,
                r.Stars,
                r.RatedAt,
                r.UserEmail,
                r.DeviceIdentifier,
                r.UserIp
            })
            .ToListAsync();

        return Ok(ratings);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteLocation(int id)
    {
        Console.WriteLine($"[API] Request to delete Location ID: {id}");
        var location = await _context.Locations.FindAsync(id);
        if (location is null)
        {
            Console.WriteLine($"[API] Location ID {id} not found.");
            return NotFound();
        }

        try {
            // 0. Xóa ScanLogs và Ratings liên quan để tránh DbUpdateException
            try {
                var scanLogs = await _context.ScanLogs.Where(sl => sl.LocationId == id).ToListAsync();
                _context.ScanLogs.RemoveRange(scanLogs);
            } catch (Exception ex) { 
                Console.WriteLine($"[API] Note: Could not delete ScanLogs (maybe table missing): {ex.Message}");
            }
            
            try {
                var ratings = await _context.Ratings.Where(r => r.LocationId == id).ToListAsync();
                _context.Ratings.RemoveRange(ratings);
            } catch (Exception ex) {
                Console.WriteLine($"[API] Note: Could not delete Ratings (maybe table missing): {ex.Message}");
            }

            // 1. Xoá TourLocations (Ràng buộc cứng)
            var tourLocations = await _context.TourLocations.Where(tl => tl.LocationId == id).ToListAsync();
            _context.TourLocations.RemoveRange(tourLocations);

            // 2. Xoá Localizations (Bản dịch/Audio)
            var localizations = await _context.Localizations.Where(l => l.LocationId == id).ToListAsync();
            _context.Localizations.RemoveRange(localizations);

            // 3. Xoá chính Location
            _context.Locations.Remove(location);
            
            await _context.SaveChangesAsync();
            InvalidateLocationsCache();
            _logger.LogInformation("Location ID {Id} deleted successfully.", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] ERROR deleting Location ID {id}: {ex.Message}");
            return StatusCode(500, ex.Message);
        }
    }

    private static double CalculateHaversineDistanceMeters(double lat1, double lng1, double lat2, double lng2)
    {
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLng = DegreesToRadians(lng2 - lng1);

        var a = Math.Pow(Math.Sin(dLat / 2), 2)
                + Math.Cos(DegreesToRadians(lat1))
                * Math.Cos(DegreesToRadians(lat2))
                * Math.Pow(Math.Sin(dLng / 2), 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * (Math.PI / 180.0);
    }
}
