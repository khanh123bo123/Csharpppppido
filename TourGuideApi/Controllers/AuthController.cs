using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TourGuideApi.Data;
using TourGuideApi.Models;

namespace TourGuideApi.Controllers;

/// <summary>
/// Admin authentication and user management
/// Handles JWT token generation and role-based access control
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext context, IConfiguration config, ILogger<AuthController> logger)
    {
        _context = context;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Get quick counts for dashboard
    /// GET: api/auth/stats
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<AuthStats>> GetStats()
    {
        var totalUsers = await _context.Users.CountAsync();
        var activeUsers = await _context.Users.CountAsync(u => u.IsActive);
        
        return Ok(new AuthStats
        {
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers
        });
    }

    /// <summary>
    /// Login endpoint for admin users
    /// POST: api/auth/login
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Email and password are required");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .Where(u => u.Email == normalizedEmail && u.IsActive)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            _logger.LogWarning($"Login attempt with unknown email: {normalizedEmail}");
            return Unauthorized("Invalid email or password");
        }

        // Verify password using BCrypt
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning($"Failed login attempt for user: {normalizedEmail}");
            return Unauthorized("Invalid email or password");
        }

        // Generate JWT token
        var token = GenerateJwtToken(user);
        user.LastTokenIssuedAt = DateTime.UtcNow;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"User logged in: {user.Email} ({user.Role})");

        return Ok(new LoginResponse
        {
            Token = token,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role
            }
        });
    }

    /// <summary>
    /// Register a new admin user (admin only)
    /// POST: api/auth/register
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<UserDto>> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Email và mật khẩu không được để trống." });
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(new { message = "Họ tên không được để trống." });
        }

        if (request.Password.Length < 6)
        {
            return BadRequest(new { message = "Mật khẩu phải có ít nhất 6 ký tự." });
        }

        // Normalize email: trim whitespace and convert to lowercase
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // Check if user already exists (case-insensitive via normalized email)
        var existingUser = await _context.Users
            .Where(u => u.Email == normalizedEmail)
            .FirstOrDefaultAsync();

        if (existingUser != null)
        {
            if (!existingUser.IsActive)
            {
                // Reactivate a previously deactivated account
                existingUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                existingUser.FullName = request.FullName.Trim();
                existingUser.IsActive = true;
                existingUser.UpdatedAt = DateTime.UtcNow;
                _context.Users.Update(existingUser);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Reactivated user: {existingUser.Email}");

                return CreatedAtAction(nameof(GetUser), new { id = existingUser.Id }, new UserDto
                {
                    Id = existingUser.Id,
                    Email = existingUser.Email,
                    FullName = existingUser.FullName,
                    Role = existingUser.Role
                });
            }

            return BadRequest(new { message = "Email này đã được đăng ký. Vui lòng sử dụng email khác." });
        }

        var user = new User
        {
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FullName = request.FullName.Trim(),
            Role = request.Role ?? "Viewer",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"New user registered: {user.Email} with role {user.Role}");

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role
        });
    }

    /// <summary>
    /// Get current user info from JWT token
    /// GET: api/auth/me
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
        {
            return Unauthorized("Invalid token");
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null || !user.IsActive)
        {
            return Unauthorized("User not found");
        }

        return Ok(new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role
        });
    }

    /// <summary>
    /// Get user by ID (admin only)
    /// GET: api/auth/users/123
    /// </summary>
    [HttpGet("users/{id}")]
    public async Task<ActionResult<UserDto>> GetUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        return Ok(new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role
        });
    }

    /// <summary>
    /// Get all users (admin only)
    /// GET: api/auth/users
    /// </summary>
    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAllUsers()
    {
        var users = await _context.Users
            .Select(u => new UserDto
            {
                Id = u.Id,
                Email = u.Email,
                FullName = u.FullName,
                Role = u.Role
            })
            .ToListAsync();

        return Ok(users);
    }

    /// <summary>
    /// Verify if the current token is valid
    /// GET: api/auth/verify
    /// </summary>
    [HttpGet("verify")]
    public ActionResult<TokenValidationResponse> VerifyToken()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        var roleClaim = User.FindFirst(ClaimTypes.Role);

        if (userIdClaim == null)
        {
            return Unauthorized("Invalid token");
        }

        return Ok(new TokenValidationResponse
        {
            IsValid = true,
            UserId = int.Parse(userIdClaim.Value),
            Role = roleClaim?.Value ?? "Viewer"
        });
    }

    private string GenerateJwtToken(User user)
    {
        var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("JWT key not configured");
        var jwtIssuer = _config["Jwt:Issuer"] ?? "TourGuideApi";
        var jwtAudience = _config["Jwt:Audience"] ?? "TourGuideApp";
        var jwtExpirationMinutes = int.Parse(_config["Jwt:ExpirationMinutes"] ?? "1440");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(jwtExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>
/// DTOs for authentication
/// </summary>
public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public UserDto User { get; set; } = new();
}

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Role { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class TokenValidationResponse
{
    public bool IsValid { get; set; }
    public int UserId { get; set; }
    public string Role { get; set; } = string.Empty;
}

public class AuthStats
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
}
