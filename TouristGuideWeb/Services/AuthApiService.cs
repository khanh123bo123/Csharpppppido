using System.Net.Http.Json;
using TouristGuideWeb.Models;

namespace TouristGuideWeb.Services;

public class AuthApiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public AuthApiService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
        
        var apiBaseUrl = _config["ApiSettings:BaseUrl"] ?? "http://localhost:5214/";
        _httpClient.BaseAddress = new Uri(apiBaseUrl);
    }

    public async Task<List<MobileUserModel>> GetAllUsersAsync(CancellationToken ct = default)
    {
        try
        {
            var users = await _httpClient.GetFromJsonAsync<List<MobileUserModel>>("api/auth/users", ct);
            return users ?? new List<MobileUserModel>();
        }
        catch
        {
            return new List<MobileUserModel>();
        }
    }

    public async Task<AuthStatsModel?> GetStatsAsync(CancellationToken ct = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<AuthStatsModel>("api/auth/stats", ct);
        }
        catch
        {
            return null;
        }
    }
}

public class MobileUserModel
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class AuthStatsModel
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
}
