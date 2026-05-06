using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QRCoder;
using TouristGuideWeb.Models;
using TouristGuideWeb.Services;

namespace TouristGuideWeb.Controllers;

[Authorize(Roles = "Admin,Owner")]
public class LocationsController : Controller
{
    private readonly LocationApiService _locationApiService;

    public LocationsController(LocationApiService locationApiService)
    {
        _locationApiService = locationApiService;
    }

    public async Task<IActionResult> Index(string? searchString, string? category, CancellationToken cancellationToken)
    {
        ViewData["CurrentFilter"] = searchString;
        ViewData["CurrentCategory"] = category;
        ViewBag.Categories = await _locationApiService.GetCategoriesAsync(cancellationToken);

        var locations = await _locationApiService.GetAllAsync(cancellationToken, searchString, category);
        
        if (User.IsInRole("Owner") && !User.IsInRole("Admin"))
        {
            locations = locations.Where(l => string.Equals(l.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        
        return View(locations);
    }

    public async Task<IActionResult> IndexMap(string? searchString, string? category, CancellationToken cancellationToken)
    {
        ViewData["CurrentCategory"] = category;
        var locations = await _locationApiService.GetAllAsync(cancellationToken, searchString, category);
        if (User.IsInRole("Owner") && !User.IsInRole("Admin"))
        {
            locations = locations.Where(l => string.Equals(l.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        return View(locations);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new LocationCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LocationCreateViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Chuyển đổi chuỗi tọa độ sang double một cách an toàn
        if (!double.TryParse(model.Latitude.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lat) ||
            !double.TryParse(model.Longitude.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lng))
        {
            ModelState.AddModelError(string.Empty, "Định dạng tọa độ không hợp lệ.");
            return View(model);
        }

        var request = new Location
        {
            Name = model.Name,
            Description = model.Description,
            Latitude = lat,
            Longitude = lng,
            Category = model.Category,
            PhoneNumber = model.PhoneNumber,
            Address = model.Address,
            ImageUrl = model.ImageUrl,
            OwnerEmail = User.Identity?.Name,
            CreatedAt = DateTime.Now
        };

        var (created, createErrorMessage) = await _locationApiService.CreateLocationAsync(request, cancellationToken);
        if (created is null)
        {
            TempData["ErrorMessage"] = "Them moi that bai. Vui long thu lai.";
            ModelState.AddModelError(
                string.Empty,
                string.IsNullOrWhiteSpace(createErrorMessage)
                    ? "Could not create location. Please try again."
                    : createErrorMessage);
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

        if (User.IsInRole("Owner") && !User.IsInRole("Admin") && !string.Equals(location.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        return View(new LocationEditViewModel
        {
            Id = location.Id,
            Name = location.Name,
            Description = location.Description,
            Latitude = location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Longitude = location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Category = location.Category,
            PhoneNumber = location.PhoneNumber,
            Address = location.Address,
            ImageUrl = location.ImageUrl
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, LocationEditViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existing = await _locationApiService.GetLocationByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        if (User.IsInRole("Owner") && !User.IsInRole("Admin") && !string.Equals(existing.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        if (!double.TryParse(model.Latitude.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lat) ||
            !double.TryParse(model.Longitude.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lng))
        {
            ModelState.AddModelError(string.Empty, "Định dạng tọa độ không hợp lệ.");
            return View(model);
        }

        existing.Name = model.Name;
        existing.Description = model.Description;
        existing.Latitude = lat;
        existing.Longitude = lng;
        existing.Category = model.Category;
        existing.PhoneNumber = model.PhoneNumber;
        existing.Address = model.Address;
        existing.ImageUrl = model.ImageUrl;
        // Do not update OwnerEmail arbitrarily


        var (updated, updateErrorMessage) = await _locationApiService.UpdateLocationAsync(id, existing, cancellationToken);
        if (!updated)
        {
            TempData["ErrorMessage"] = "Cap nhat that bai. Vui long thu lai.";
            ModelState.AddModelError(
                string.Empty,
                string.IsNullOrWhiteSpace(updateErrorMessage)
                    ? "Could not update location. Please try again."
                    : updateErrorMessage);
            return View(model);
        }

        TempData["SuccessMessage"] = "Cap nhat dia diem thanh cong.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var existing = await _locationApiService.GetLocationByIdAsync(id, cancellationToken);
        if (existing == null) return NotFound();

        if (User.IsInRole("Owner") && !User.IsInRole("Admin") && !string.Equals(existing.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var deleted = await _locationApiService.DeleteLocationAsync(id, cancellationToken);
        TempData[deleted ? "SuccessMessage" : "ErrorMessage"] = deleted
            ? "Xoa dia diem thanh cong."
            : "Xoa that bai. Vui long thu lai.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReprocessAll(CancellationToken cancellationToken)
    {
        var success = await _locationApiService.ReprocessTranslationsAsync(cancellationToken);
        if (success)
        {
            TempData["SuccessMessage"] = "Đã gửi yêu cầu dịch lại toàn bộ các địa điểm. Quá trình này sẽ chạy ngầm và mất vài phút để hoàn tất.";
        }
        else
        {
            TempData["ErrorMessage"] = "Không thể gửi yêu cầu dịch lại. Vui lòng kiểm tra API.";
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var location = await _locationApiService.GetLocationByIdAsync(id, cancellationToken);
        if (location is null)
        {
            return NotFound();
        }

        if (User.IsInRole("Owner") && !User.IsInRole("Admin") && !string.Equals(location.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
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

    [HttpGet]
    public async Task<IActionResult> ExportQrPdf([FromQuery] List<int>? ids, CancellationToken cancellationToken)
    {
        var allLocations = await _locationApiService.GetAllAsync(cancellationToken);

        var selectedLocations = ids is { Count: > 0 }
            ? allLocations.Where(location => ids.Contains(location.Id)).ToList()
            : allLocations.ToList();

        if (selectedLocations.Count == 0)
        {
            TempData["ErrorMessage"] = "Khong co dia diem de xuat PDF.";
            return RedirectToAction(nameof(Index));
        }

        QuestPDF.Settings.License = LicenseType.Community;
        var pdfBytes = BuildQrPdf(selectedLocations);
        var fileName = $"locations-qr-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";

        return File(pdfBytes, "application/pdf", fileName);
    }

    private static byte[] BuildQrPdf(IReadOnlyList<Location> locations)
    {
        var locationChunks = locations.Chunk(6).ToList();

        return Document
            .Create(document =>
            {
                foreach (var chunk in locationChunks)
                {
                    document.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(20);
                        page.DefaultTextStyle(style => style.FontSize(11));

                        page.Header()
                            .Text("Danh sach QR dia diem")
                            .SemiBold()
                            .FontSize(16);

                        page.Content().Column(column =>
                        {
                            for (var rowIndex = 0; rowIndex < 3; rowIndex++)
                            {
                                var leftCell = rowIndex * 2 < chunk.Length ? chunk[rowIndex * 2] : null;
                                var rightCell = rowIndex * 2 + 1 < chunk.Length ? chunk[rowIndex * 2 + 1] : null;

                                column.Item().Row(row =>
                                {
                                    row.RelativeItem().Element(container => ComposeLocationCell(container, leftCell));
                                    row.Spacing(8);
                                    row.RelativeItem().Element(container => ComposeLocationCell(container, rightCell));
                                });

                                if (rowIndex < 2)
                                {
                                    column.Item().PaddingTop(8);
                                }
                            }
                        });

                        page.Footer().AlignRight().Text(text =>
                        {
                            text.Span("Trang ");
                            text.CurrentPageNumber();
                            text.Span(" / ");
                            text.TotalPages();
                        });
                    });
                }
            })
            .GeneratePdf();
    }

    private static void ComposeLocationCell(IContainer container, Location? location)
    {
        container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(8)
            .MinHeight(240)
            .Column(column =>
            {
                if (location is null)
                {
                    column.Item().AlignCenter().Text(string.Empty);
                    return;
                }

                var qrBytes = GenerateQrCodeBytes(location.QrCodeData);

                column.Item().AlignCenter().Text(location.Name).SemiBold();
                column.Item().PaddingTop(6).AlignCenter().Width(130).Image(qrBytes);
                column.Item().PaddingTop(6).AlignCenter().Text($"Lat: {location.Latitude:0.######}");
                column.Item().AlignCenter().Text($"Lng: {location.Longitude:0.######}");
            });
    }

    private static byte[] GenerateQrCodeBytes(string? qrData)
    {
        var value = string.IsNullOrWhiteSpace(qrData) ? Guid.NewGuid().ToString("N") : qrData;

        using var qrGenerator = new QRCodeGenerator();
        using var codeData = qrGenerator.CreateQrCode(value, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(codeData);
        return qrCode.GetGraphic(20);
    }
}
