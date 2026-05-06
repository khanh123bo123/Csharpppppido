using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace TouristGuideApp.Services;

public interface IAuthService
{
    bool IsLoggedIn { get; }
    string? UserEmail { get; }
    string? UserFullName { get; }
    string? UserRole { get; }
    
    Task<bool> LoginAsync(string email, string password);
    Task<(bool Success, string? ErrorMessage)> LoginWithDetailsAsync(string email, string password);
    Task<(bool Success, string? ErrorMessage)> RegisterAsync(string email, string password, string fullName);
    Task LogoutAsync();
    Task<bool> CheckAuthStatusAsync();
}

public class AuthService : IAuthService
{
    private readonly IApiService _apiService;
    private const string TokenKey = "auth_token";

    public bool IsLoggedIn { get; private set; }
    public string? UserEmail { get; private set; }
    public string? UserFullName { get; private set; }
    public string? UserRole { get; private set; }

    public AuthService(IApiService apiService)
    {
        _apiService = apiService;
    }

    public async Task<bool> CheckAuthStatusAsync()
    {
        try
        {
            var token = await SecureStorage.GetAsync(TokenKey);
            if (string.IsNullOrEmpty(token))
            {
                IsLoggedIn = false;
                return false;
            }

            var isValid = await _apiService.VerifyTokenAsync(token);
            if (isValid)
            {
                ParseToken(token);
                IsLoggedIn = true;
                return true;
            }

            // Token invalid or expired
            await LogoutAsync();
            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        var result = await LoginWithDetailsAsync(email, password);
        return result.Success;
    }

    public async Task<(bool Success, string? ErrorMessage)> LoginWithDetailsAsync(string email, string password)
    {
        var (token, errorMessage) = await _apiService.LoginWithDetailsAsync(email, password);
        if (!string.IsNullOrEmpty(token))
        {
            await SecureStorage.SetAsync(TokenKey, token);
            ParseToken(token);
            IsLoggedIn = true;
            return (true, null);
        }
        return (false, errorMessage);
    }

    public async Task<(bool Success, string? ErrorMessage)> RegisterAsync(string email, string password, string fullName)
    {
        return await _apiService.RegisterAsync(email, password, fullName);
    }

    public async Task LogoutAsync()
    {
        SecureStorage.Remove(TokenKey);
        IsLoggedIn = false;
        UserEmail = null;
        UserFullName = null;
        UserRole = null;
        await Task.CompletedTask;
    }

    private void ParseToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            
            UserEmail = jsonToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email || c.Type == "email")?.Value;
            UserFullName = jsonToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name || c.Type == "unique_name")?.Value;
            UserRole = jsonToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type == "role")?.Value;
        }
        catch
        {
            // Fail silently
        }
    }
}
