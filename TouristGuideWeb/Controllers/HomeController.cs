using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TouristGuideWeb.Models;
using Microsoft.AspNetCore.Authorization;
using TouristGuideWeb.Services;

namespace TouristGuideWeb.Controllers;

[AllowAnonymous]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly LocationApiService _locationApiService;

    public HomeController(ILogger<HomeController> logger, LocationApiService locationApiService)
    {
        _logger = logger;
        _locationApiService = locationApiService;
    }

    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }
        
        ViewData["IsLandingPage"] = true;
        return View();
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Analytics(CancellationToken cancellationToken)
    {
        var logs = await _locationApiService.GetScanLogsAsync(cancellationToken);
        return View(logs);
    }

    public IActionResult Download()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
