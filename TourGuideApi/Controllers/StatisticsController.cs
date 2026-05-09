using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using TourGuideApi.Data;

namespace TourGuideApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatisticsController : ControllerBase
{
    private readonly AppDbContext _context;

    public StatisticsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboardStatistics()
    {
        // 1. Most played audios
        var topAudios = await _context.Localizations
            .Where(l => l.PlayCount > 0)
            .OrderByDescending(l => l.PlayCount)
            .Take(5)
            .Select(l => new
            {
                LocationName = l.Location.Name,
                LanguageCode = l.LanguageCode,
                PlayCount = l.PlayCount
            })
            .ToListAsync();

        // 2. Plays by hour (in UTC+7 for Vietnam)
        // Since we just added the log, we can pull all logs for the last 30 days
        // Or if the table is huge, we'd do a group by in SQL. But SQLite/PostgreSQL have different date functions.
        // The safest cross-db way for small datasets is to pull the dates, or use basic group by if we use Npgsql.
        // Assuming PostgreSQL is used (based on previous files)
        
        // We will just fetch recent logs and group in memory to avoid DB differences for this simple task
        var recentLogs = await _context.AudioPlayLogs
            .Where(l => l.PlayedAt >= DateTime.UtcNow.AddDays(-30))
            .Select(l => l.PlayedAt)
            .ToListAsync();

        var playsByHour = new int[24];
        foreach (var utcTime in recentLogs)
        {
            // Convert to UTC+7
            var localTime = utcTime.AddHours(7);
            playsByHour[localTime.Hour]++;
        }

        return Ok(new
        {
            TopAudios = topAudios,
            PlaysByHour = playsByHour
        });
    }
}
