using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourGuideApi.Data;
using TourGuideApi.Models;
using TourGuideApi.Services;

namespace TourGuideApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LocationsController : ControllerBase
{
    private const double EarthRadiusMeters = 6371000;
    private readonly AppDbContext _context;
    private readonly ITextToSpeechService _textToSpeechService;

    public LocationsController(AppDbContext context, ITextToSpeechService textToSpeechService)
    {
        _context = context;
        _textToSpeechService = textToSpeechService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Location>>> GetLocations()
    {
        return await _context.Locations.ToListAsync();
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
    public async Task<ActionResult<Location>> GetLocationByQr([FromQuery] string code)
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

        if (!string.IsNullOrWhiteSpace(location.Description))
        {
            try
            {
                location.AudioUrl = await _textToSpeechService.SynthesizeAsync(
                    location.Description,
                    $"location_{location.Id}.mp3");

                await _context.SaveChangesAsync();
            }
            catch
            {
                // Keep location creation successful even when TTS is unavailable.
            }
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

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteLocation(int id)
    {
        var location = await _context.Locations.FindAsync(id);
        if (location is null)
        {
            return NotFound();
        }

        _context.Locations.Remove(location);
        await _context.SaveChangesAsync();

        return NoContent();
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
