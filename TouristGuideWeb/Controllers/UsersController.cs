using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace TouristGuideWeb.Controllers;

[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public UsersController(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _userManager.Users.ToListAsync();
        
        var userRolesMap = new Dictionary<string, IList<string>>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userRolesMap.Add(user.Id, roles);
        }

        ViewBag.UserRolesMap = userRolesMap;
        return View(users);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string email, string password, string role)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError("", "Email và Mật khẩu là bắt buộc.");
            return View();
        }

        var user = new IdentityUser { UserName = email, Email = email };
        var result = await _userManager.CreateAsync(user, password);

        if (result.Succeeded)
        {
            if (!string.IsNullOrWhiteSpace(role))
            {
                await _userManager.AddToRoleAsync(user, role);
            }
            TempData["SuccessMessage"] = "Đã tạo tài khoản thành công!";
            return RedirectToAction(nameof(Index));
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError("", error.Description);

        return View();
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (id == null) return NotFound();
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        ViewBag.CurrentRole = roles.FirstOrDefault();

        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, string role, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        // Update Role
        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        if (!string.IsNullOrWhiteSpace(role))
        {
            await _userManager.AddToRoleAsync(user, role);
        }

        // Update Password
        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await _userManager.ResetPasswordAsync(user, token, newPassword);
            if (!resetResult.Succeeded)
            {
                foreach (var err in resetResult.Errors) ModelState.AddModelError("", err.Description);
                ViewBag.CurrentRole = role;
                return View(user);
            }
        }

        TempData["SuccessMessage"] = "Đã cập nhật tài khoản!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user != null)
        {
            if (user.Email == "pipizxc05@gmail.com") // Protect super admin
            {
                TempData["ErrorMessage"] = "Không thể xóa tài khoản hệ thống này!";
                return RedirectToAction(nameof(Index));
            }
            await _userManager.DeleteAsync(user);
            TempData["SuccessMessage"] = "Đã xóa tài khoản!";
        }
        return RedirectToAction(nameof(Index));
    }
}
