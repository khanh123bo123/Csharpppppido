using Microsoft.AspNetCore.Mvc;
using TourGuideApi.Services;
using TourGuideApi.Models;

namespace TourGuideApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    private readonly IOnlineTracker _onlineTracker;
    private readonly TourGuideApi.Data.AppDbContext _context;

    public StatusController(IOnlineTracker onlineTracker, TourGuideApi.Data.AppDbContext context)
    {
        _onlineTracker = onlineTracker;
        _context = context;
    }

    [HttpGet("online-count")]
    public IActionResult GetOnlineCount()
    {
        var count = _onlineTracker.GetOnlineCount();
        return Ok(new { onlineCount = count });
    }

    [HttpGet("listen-stats")]
    public IActionResult GetListenStats()
    {
        var total = _context.ListenLogs.Count();
        return Ok(new { totalListens = total });
    }

    [HttpPost("log-listen")]
    public async Task<IActionResult> LogListen([FromQuery] int locationId, [FromQuery] string languageCode, [FromQuery] string? deviceId)
    {
        var log = new ListenLog
        {
            LocationId = locationId,
            LanguageCode = languageCode,
            DeviceId = deviceId,
            ListenedAt = DateTime.UtcNow
        };
        _context.ListenLogs.Add(log);
        await _context.SaveChangesAsync();
        return Ok(new { status = "logged" });
    }

    [HttpPost("heartbeat")]
    public IActionResult PostHeartbeat([FromQuery] string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return BadRequest("DeviceId is required.");
        }

        _onlineTracker.MarkAsOnline(deviceId);
        return Ok(new { status = "success", timestamp = DateTime.UtcNow });
    }
}
