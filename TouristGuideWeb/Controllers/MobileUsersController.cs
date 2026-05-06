using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TouristGuideWeb.Services;

namespace TouristGuideWeb.Controllers;

[Authorize(Roles = "Admin")]
public class MobileUsersController : Controller
{
    private readonly AuthApiService _authApiService;

    public MobileUsersController(AuthApiService authApiService)
    {
        _authApiService = authApiService;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var users = await _authApiService.GetAllUsersAsync(ct);
        return View(users);
    }
}
