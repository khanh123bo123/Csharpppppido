using Microsoft.AspNetCore.Mvc;

namespace TourGuideApi.Controllers;

/// <summary>
/// Lightweight health endpoint for mobile/dev connectivity checks.
/// </summary>
[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public ActionResult<object> Get()
    {
        return Ok(new
        {
            status = "ok",
            utc = DateTime.UtcNow
        });
    }
}
