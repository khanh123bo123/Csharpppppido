using Microsoft.AspNetCore.Mvc;
using QRCoder;
using TouristGuideWeb.Models;
using TouristGuideWeb.Services;

namespace TouristGuideWeb.Controllers;

public class LocationsController : Controller
{
    private readonly LocationApiService _locationApiService;

    public LocationsController(LocationApiService locationApiService)
    {
        _locationApiService = locationApiService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var locations = await _locationApiService.GetAllAsync(cancellationToken);
        return View(locations);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new Location());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Location model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var created = await _locationApiService.CreateLocationAsync(model, cancellationToken);
        if (created is null)
        {
            TempData["ErrorMessage"] = "Them moi that bai. Vui long thu lai.";
            ModelState.AddModelError(string.Empty, "Could not create location. Please try again.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Them dia diem thanh cong.";
        TempData["QrCodeData"] = created.QrCodeData;
        return RedirectToAction(nameof(Details), new { id = created.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var location = await _locationApiService.GetLocationByIdAsync(id, cancellationToken);
        if (location is null)
        {
            return NotFound();
        }

        return View(location);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Location model, CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var updated = await _locationApiService.UpdateLocationAsync(id, model, cancellationToken);
        if (!updated)
        {
            TempData["ErrorMessage"] = "Cap nhat that bai. Vui long thu lai.";
            ModelState.AddModelError(string.Empty, "Could not update location. Please try again.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Cap nhat dia diem thanh cong.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _locationApiService.DeleteLocationAsync(id, cancellationToken);
        TempData[deleted ? "SuccessMessage" : "ErrorMessage"] = deleted
            ? "Xoa dia diem thanh cong."
            : "Xoa that bai. Vui long thu lai.";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var location = await _locationApiService.GetLocationByIdAsync(id, cancellationToken);
        if (location is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(location.QrCodeData) && TempData.TryGetValue("QrCodeData", out var qrData))
        {
            location.QrCodeData = qrData?.ToString() ?? string.Empty;
        }

        return View(location);
    }

    public IActionResult GenerateQr(string qrData)
    {
        if (string.IsNullOrWhiteSpace(qrData))
        {
            return BadRequest("qrData is required.");
        }

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrCodeData);
        var bytes = qrCode.GetGraphic(20);

        return File(bytes, "image/png");
    }
}
