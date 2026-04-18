using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TouristGuideWeb.Controllers;

[Authorize(Roles = "Admin,Owner")]
public class PrdController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult UseCase()
    {
        return View();
    }
}
