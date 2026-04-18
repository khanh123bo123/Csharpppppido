using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourGuideApi.Data;
using TourGuideApi.Models;

namespace TourGuideApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ToursController : ControllerBase
{
    private readonly AppDbContext _context;

    public ToursController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Tour>>> GetTours()
    {
        return await _context.Tours.OrderByDescending(t => t.CreatedAt).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Tour>> GetTour(int id)
    {
        var tour = await _context.Tours.FindAsync(id);
        if (tour == null) return NotFound();
        return tour;
    }

    [HttpGet("{id}/locations")]
    public async Task<ActionResult<IEnumerable<TourLocation>>> GetTourLocations(int id)
    {
        return await _context.TourLocations
            .Include(tl => tl.Location)
            .Where(tl => tl.TourId == id)
            .OrderBy(tl => tl.OrderIndex)
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<Tour>> PostTour(Tour tour)
    {
        tour.CreatedAt = DateTime.UtcNow;
        tour.UpdatedAt = DateTime.UtcNow;
        _context.Tours.Add(tour);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetTour), new { id = tour.Id }, tour);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutTour(int id, Tour tour)
    {
        if (id != tour.Id) return BadRequest();
        
        var existing = await _context.Tours.FindAsync(id);
        if (existing == null) return NotFound();

        existing.Name = tour.Name;
        existing.Description = tour.Description;
        existing.EstimatedDistanceKm = tour.EstimatedDistanceKm;
        existing.EstimatedDurationMinutes = tour.EstimatedDurationMinutes;
        existing.IsActive = tour.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;
        
        if (!string.IsNullOrEmpty(tour.OwnerEmail))
            existing.OwnerEmail = tour.OwnerEmail;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTour(int id)
    {
        var tour = await _context.Tours.FindAsync(id);
        if (tour == null) return NotFound();

        _context.Tours.Remove(tour);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // Add or remove locations from tour
    [HttpPost("{id}/locations/{locationId}")]
    public async Task<IActionResult> AddLocationToTour(int id, int locationId, [FromQuery] int orderIndex = 0)
    {
        if (await _context.TourLocations.AnyAsync(tl => tl.TourId == id && tl.LocationId == locationId))
            return BadRequest("Location is already in this tour.");

        var tl = new TourLocation
        {
            TourId = id,
            LocationId = locationId,
            OrderIndex = orderIndex
        };
        _context.TourLocations.Add(tl);
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id}/locations/{locationId}")]
    public async Task<IActionResult> RemoveLocationFromTour(int id, int locationId)
    {
        var tl = await _context.TourLocations.FirstOrDefaultAsync(x => x.TourId == id && x.LocationId == locationId);
        if (tl == null) return NotFound();

        _context.TourLocations.Remove(tl);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // Reorder locations within a tour (swap / bulk update OrderIndex)
    [HttpPut("{id}/locations/reorder")]
    public async Task<IActionResult> ReorderLocations(int id, [FromBody] List<ReorderItem> items)
    {
        if (items == null || items.Count == 0) return BadRequest("Items list is required.");

        var tourLocations = await _context.TourLocations
            .Where(tl => tl.TourId == id)
            .ToListAsync();

        foreach (var item in items)
        {
            var tl = tourLocations.FirstOrDefault(x => x.LocationId == item.LocationId);
            if (tl != null)
            {
                tl.OrderIndex = item.OrderIndex;
            }
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }
}

public class ReorderItem
{
    public int LocationId { get; set; }
    public int OrderIndex { get; set; }
}
