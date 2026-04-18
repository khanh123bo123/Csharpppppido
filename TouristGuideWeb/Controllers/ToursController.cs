using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TouristGuideWeb.Models;
using TouristGuideWeb.Services;

namespace TouristGuideWeb.Controllers;

[Authorize(Roles = "Admin,Owner")]
public class ToursController : Controller
{
    private readonly TourApiService _tourApiService;
    private readonly LocationApiService _locationApiService;

    public ToursController(TourApiService tourApiService, LocationApiService locationApiService)
    {
        _tourApiService = tourApiService;
        _locationApiService = locationApiService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var tours = await _tourApiService.GetAllAsync(cancellationToken);
        if (User.IsInRole("Owner") && !User.IsInRole("Admin"))
        {
            tours = tours.Where(t => string.Equals(t.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        return View(tours);
    }

    public IActionResult Create()
    {
        return View(new Tour());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Tour model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        model.OwnerEmail = User.Identity?.Name;
        
        var created = await _tourApiService.CreateAsync(model, cancellationToken);
        if (created == null)
        {
            TempData["ErrorMessage"] = "Could not create tour.";
            return View(model);
        }

        TempData["SuccessMessage"] = "Tao lo trinh thành công.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var tour = await _tourApiService.GetByIdAsync(id, cancellationToken);
        if (tour == null) return NotFound();

        if (User.IsInRole("Owner") && !User.IsInRole("Admin") && !string.Equals(tour.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        return View(tour);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Tour model, CancellationToken cancellationToken)
    {
        if (id != model.Id) return BadRequest();

        if (!ModelState.IsValid) return View(model);

        var existing = await _tourApiService.GetByIdAsync(id, cancellationToken);
        if (existing == null) return NotFound();

        if (User.IsInRole("Owner") && !User.IsInRole("Admin") && !string.Equals(existing.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        model.OwnerEmail = existing.OwnerEmail;
        var updated = await _tourApiService.UpdateAsync(id, model, cancellationToken);
        
        if (!updated)
        {
            TempData["ErrorMessage"] = "Cap nhat that bai.";
            return View(model);
        }

        TempData["SuccessMessage"] = "Cập nhật lộ trình thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var existing = await _tourApiService.GetByIdAsync(id, cancellationToken);
        if (existing == null) return NotFound();

        if (User.IsInRole("Owner") && !User.IsInRole("Admin") && !string.Equals(existing.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var deleted = await _tourApiService.DeleteAsync(id, cancellationToken);
        TempData[deleted ? "SuccessMessage" : "ErrorMessage"] = deleted ? "Delete successful." : "Delete failed.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var tour = await _tourApiService.GetByIdAsync(id, cancellationToken);
        if (tour == null) return NotFound();

        if (User.IsInRole("Owner") && !User.IsInRole("Admin") && !string.Equals(tour.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var assignedLocations = await _tourApiService.GetLocationsAsync(id, cancellationToken);
        var allLocations = await _locationApiService.GetAllAsync(cancellationToken);

        // Remove locations that are already assigned
        var assignedLocationIds = assignedLocations.Select(tl => tl.LocationId).ToHashSet();
        var availableLocations = allLocations.Where(l => !assignedLocationIds.Contains(l.Id)).ToList();

        var viewModel = new TourDetailsViewModel
        {
            CurrentTour = tour,
            AssignedLocations = assignedLocations,
            AvailableLocations = availableLocations
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddLocation(int id, int locationId, CancellationToken cancellationToken)
    {
        var tour = await _tourApiService.GetByIdAsync(id, cancellationToken);
        if (tour == null) return NotFound();

        if (User.IsInRole("Owner") && !User.IsInRole("Admin") && !string.Equals(tour.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var assigned = await _tourApiService.GetLocationsAsync(id, cancellationToken);
        int nextOrder = assigned.Count > 0 ? assigned.Max(a => a.OrderIndex) + 1 : 1;

        var success = await _tourApiService.AddLocationAsync(id, locationId, nextOrder, cancellationToken);
        if (success) TempData["SuccessMessage"] = "Đã thêm địa điểm vào lộ trình.";
        else TempData["ErrorMessage"] = "Không thể thêm địa điểm.";

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveLocation(int id, int locationId, CancellationToken cancellationToken)
    {
        var tour = await _tourApiService.GetByIdAsync(id, cancellationToken);
        if (tour == null) return NotFound();

        if (User.IsInRole("Owner") && !User.IsInRole("Admin") && !string.Equals(tour.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var success = await _tourApiService.RemoveLocationAsync(id, locationId, cancellationToken);
        if (success) TempData["SuccessMessage"] = "Đã gỡ địa điểm khỏi lộ trình.";
        else TempData["ErrorMessage"] = "Không thể gỡ địa điểm.";

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveLocationUp(int id, int locationId, CancellationToken cancellationToken)
    {
        return await SwapLocationOrder(id, locationId, -1, cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveLocationDown(int id, int locationId, CancellationToken cancellationToken)
    {
        return await SwapLocationOrder(id, locationId, 1, cancellationToken);
    }

    private async Task<IActionResult> SwapLocationOrder(int tourId, int locationId, int direction, CancellationToken cancellationToken)
    {
        var tour = await _tourApiService.GetByIdAsync(tourId, cancellationToken);
        if (tour == null) return NotFound();

        if (User.IsInRole("Owner") && !User.IsInRole("Admin") && !string.Equals(tour.OwnerEmail, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var assigned = await _tourApiService.GetLocationsAsync(tourId, cancellationToken);
        var ordered = assigned.OrderBy(a => a.OrderIndex).ToList();

        var currentIndex = ordered.FindIndex(a => a.LocationId == locationId);
        if (currentIndex < 0)
        {
            TempData["ErrorMessage"] = "Không tìm thấy địa điểm trong lộ trình.";
            return RedirectToAction(nameof(Details), new { id = tourId });
        }

        var swapIndex = currentIndex + direction;
        if (swapIndex < 0 || swapIndex >= ordered.Count)
        {
            return RedirectToAction(nameof(Details), new { id = tourId });
        }

        // Swap OrderIndex values
        var currentItem = ordered[currentIndex];
        var swapItem = ordered[swapIndex];
        var tempOrder = currentItem.OrderIndex;

        var reorderItems = new List<ReorderItemDto>
        {
            new() { LocationId = currentItem.LocationId, OrderIndex = swapItem.OrderIndex },
            new() { LocationId = swapItem.LocationId, OrderIndex = tempOrder }
        };

        var success = await _tourApiService.ReorderAsync(tourId, reorderItems, cancellationToken);
        if (!success) TempData["ErrorMessage"] = "Không thể thay đổi thứ tự.";

        return RedirectToAction(nameof(Details), new { id = tourId });
    }
}
