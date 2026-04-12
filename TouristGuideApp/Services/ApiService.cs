using System.Net.Http.Json;
using TouristGuideApp.Models;

namespace TouristGuideApp.Services;

#nullable enable
public interface IApiService
{
    Task<List<POI>> GetPOIsAsync(CancellationToken cancellationToken = default);
    Task SyncPOIsToLocalAsync(IDatabaseService databaseService);

    // Backward compatibility
    Task<IReadOnlyList<TouristGuideApp.Models.Location>> GetLocationsAsync(CancellationToken cancellationToken = default);
    Task<TouristGuideApp.Models.Location?> GetLocationAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> CreateLocationAsync(TouristGuideApp.Models.Location location, CancellationToken cancellationToken = default);
    Task<bool> UpdateLocationAsync(TouristGuideApp.Models.Location location, CancellationToken cancellationToken = default);
    Task<bool> DeleteLocationAsync(int id, CancellationToken cancellationToken = default);

    // Localization support (multilingual)
    Task<List<LocalizationModel>> GetLocalizationsAsync(int locationId, CancellationToken cancellationToken = default);
    Task<LocalizationModel?> GetLocalizationAsync(int locationId, string languageCode, CancellationToken cancellationToken = default);
    Task<string?> GetLocalizationAudioAsync(int localizationId, CancellationToken cancellationToken = default);

    // Connectivity check
    Task<bool> PingAsync(CancellationToken cancellationToken = default);

    // Authentication
    Task<string?> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<bool> VerifyTokenAsync(string token, CancellationToken cancellationToken = default);
    
    // Tours
    Task<List<Tour>> GetToursAsync(CancellationToken cancellationToken = default);
    Task<List<TourLocation>> GetTourLocationsAsync(int tourId, CancellationToken cancellationToken = default);
    

}

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private string? _authToken;

    public ApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Set JWT token for authenticated requests
    /// </summary>
    public void SetAuthToken(string token)
    {
        _authToken = token;
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Get POIs from API (converts Location to POI)
    /// </summary>
    public async Task<List<POI>> GetPOIsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var response = await _httpClient.GetAsync("api/locations", cancellationToken);
            response.EnsureSuccessStatusCode();

            var locations = await response.Content.ReadFromJsonAsync<List<Models.Location>>(options, cancellationToken);

            if (locations == null) return new List<POI>();

            return locations.Select(l => new POI
            {
                Name = l.Name ?? "Chưa đặt tên",
                Description = l.Description ?? "Không có mô tả",
                Latitude = l.Latitude,
                Longitude = l.Longitude,
                Radius = 30,
                Category = string.IsNullOrWhiteSpace(l.Category) ? "Chưa phân loại" : l.Category,
                PhoneNumber = l.PhoneNumber,
                Address = l.Address,
                ImageUrl = l.ImageUrl,
                LanguageCode = "vi-VN",
                AudioUrl = l.AudioUrl
            }).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"API ERROR: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Sync POIs to local database (offline-first)
    /// </summary>
    public async Task SyncPOIsToLocalAsync(IDatabaseService databaseService)
    {
        try
        {
            var apiPois = await GetPOIsAsync();

            await databaseService.ClearAllPOIsAsync();

            if (apiPois != null && apiPois.Any())
            {
                foreach (var poi in apiPois)
                {
                    if (poi.Radius <= 0) poi.Radius = 100;
                    await databaseService.SavePOIAsync(poi);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Sync Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Get all localizations for a location
    /// </summary>
    public async Task<List<LocalizationModel>> GetLocalizationsAsync(int locationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var localizations = await _httpClient.GetFromJsonAsync<List<LocalizationModel>>(
                $"api/localizations/by-location/{locationId}",
                options,
                cancellationToken);
            return localizations ?? new List<LocalizationModel>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Get localizations error: {ex.Message}");
            return new List<LocalizationModel>();
        }
    }

    /// <summary>
    /// Get localization for specific language
    /// </summary>
    public async Task<LocalizationModel?> GetLocalizationAsync(int locationId, string languageCode, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return await _httpClient.GetFromJsonAsync<LocalizationModel>(
                $"api/localizations/{locationId}/{languageCode}",
                options,
                cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Download cached audio file (TIER 1)
    /// </summary>
    public async Task<string?> GetLocalizationAudioAsync(int localizationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/localizations/{localizationId}/audio", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var audioBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                // Save to device storage or return as base64
                return Convert.ToBase64String(audioBytes);
            }
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Audio download error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Ping API to check connectivity
    /// </summary>
    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/locations?limit=1", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Admin login to get JWT token
    /// </summary>
    public async Task<string?> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new { email, password };
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonDoc = System.Text.Json.JsonDocument.Parse(json);
                var token = jsonDoc.RootElement.GetProperty("token").GetString();
                
                if (token != null)
                {
                    SetAuthToken(token);
                }
                return token;
            }
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Login error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Verify JWT token
    /// </summary>
    public async Task<bool> VerifyTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthToken(token);
            var response = await _httpClient.GetAsync("api/auth/verify", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // --- Backward Compatibility Methods ---

    public async Task<IReadOnlyList<Models.Location>> GetLocationsAsync(CancellationToken cancellationToken = default)
    {
        var result = await _httpClient.GetFromJsonAsync<List<Models.Location>>("api/locations", cancellationToken);
        return result ?? new List<Models.Location>();
    }

    public Task<Models.Location?> GetLocationAsync(int id, CancellationToken cancellationToken = default)
    {
        return _httpClient.GetFromJsonAsync<Models.Location>($"api/locations/{id}", cancellationToken);
    }

    public async Task<bool> CreateLocationAsync(Models.Location location, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/locations", location, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateLocationAsync(Models.Location location, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/locations/{location.Id}", location, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteLocationAsync(int id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/locations/{id}", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    // --- Tour Methods ---
    public async Task<List<Tour>> GetToursAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<Tour>>("api/tours", cancellationToken);
            return result ?? new List<Tour>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to fetch tours: {ex.Message}");
            return new List<Tour>();
        }
    }

    public async Task<List<TourLocation>> GetTourLocationsAsync(int tourId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<TourLocation>>($"api/tours/{tourId}/locations", cancellationToken);
            return result ?? new List<TourLocation>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to fetch tour locations: {ex.Message}");
            return new List<TourLocation>();
        }
    }
}

