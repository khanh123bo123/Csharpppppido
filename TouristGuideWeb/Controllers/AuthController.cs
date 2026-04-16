using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace TouristGuideWeb.Controllers;

public class AuthController : Controller
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;

    public AuthController(SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string role = "Admin", string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        ViewBag.Role = role;
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string? email, string? password, bool rememberMe, string role = "Admin", string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/Dashboard");

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError(string.Empty, "Tài khoản và mật khẩu không được bỏ trống.");
            ViewBag.Role = role;
            return View();
        }

        // Verify role before logging in
        var user = await _userManager.FindByEmailAsync(email);
        if (user != null)
        {
            var isRoleCorrect = await _userManager.IsInRoleAsync(user, role);
            if (!isRoleCorrect)
            {
                ModelState.AddModelError(string.Empty, $"Tài khoản này không có quyền truy cập cổng {role}.");
                ViewBag.Role = role;
                return View();
            }
        }

        var result = await _signInManager.PasswordSignInAsync(user != null ? user.UserName! : email!, password!, rememberMe, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            return LocalRedirect(returnUrl);
        }

        ModelState.AddModelError(string.Empty, "Tài khoản hoặc mật khẩu không đúng.");
        ViewBag.Role = role;
        return View();
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string? storeName, string? email, string? password, string? confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirmPassword))
        {
            ModelState.AddModelError(string.Empty, "Vui lòng điền đầy đủ thông tin.");
            return View();
        }

        if (password != confirmPassword)
        {
            ModelState.AddModelError(string.Empty, "Mật khẩu xác nhận không khớp.");
            return View();
        }

        // Use the Store Name / Display Name as the Identity UserName, fallback to email if empty
        string userName = !string.IsNullOrWhiteSpace(storeName) ? storeName : email;
        var user = new IdentityUser { UserName = userName, Email = email };
        var result = await _userManager.CreateAsync(user, password);

        if (result.Succeeded)
        {
            // Auto assign as Owner since Admin is manual/seeded
            await _userManager.AddToRoleAsync(user, "Owner");
            
            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Dashboard");
        }

        foreach (var error in result.Errors)
        {
            string message = error.Code switch
            {
                "PasswordRequiresNonAlphanumeric" => "Mật khẩu phải chứa ký tự đặc biệt (vd: @, #, $...).",
                "PasswordRequiresDigit" => "Mật khẩu phải chứa ít nhất một chữ số (0-9).",
                "PasswordRequiresLower" => "Mật khẩu phải chứa ít nhất một chữ cái thường (a-z).",
                "PasswordRequiresUpper" => "Mật khẩu phải chứa ít nhất một chữ cái in hoa (A-Z).",
                "PasswordTooShort" => "Mật khẩu phải có độ dài tối thiểu 6 ký tự.",
                "DuplicateUserName" => "Tên đăng nhập hoặc Email này đã được sử dụng.",
                "DuplicateEmail" => "Email này đã được sử dụng.",
                "InvalidEmail" => "Định dạng Email không hợp lệ.",
                "InvalidUserName" => "Tên đăng nhập không hợp lệ.",
                _ => error.Description
            };
            ModelState.AddModelError(string.Empty, message);
        }

        return View();
    }

    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword()
    {
        return View();
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string? currentPassword, string? newPassword, string? confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
        {
            ModelState.AddModelError(string.Empty, "Vui lòng điền đầy đủ mật khẩu.");
            return View();
        }

        if (newPassword != confirmPassword)
        {
            ModelState.AddModelError(string.Empty, "Mật khẩu xác nhận không khớp.");
            return View();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
            return RedirectToAction("Index", "Dashboard");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }
}
