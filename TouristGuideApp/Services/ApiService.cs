using System.Net.Http.Json;
using TouristGuideApp.Models;

namespace TouristGuideApp.Services;

#nullable enable
public interface IApiService
{
    Task<List<POI>> GetPOIsAsync(CancellationToken cancellationToken = default, string? query = null);
    Task SyncPOIsToLocalAsync(IDatabaseService databaseService, string? languageCode = null);

    // Backward compatibility
    Task<IReadOnlyList<TouristGuideApp.Models.Location>> GetLocationsAsync(CancellationToken cancellationToken = default, string? query = null);
    Task<TouristGuideApp.Models.Location?> GetLocationAsync(int id, CancellationToken cancellationToken = default);
    Task<TouristGuideApp.Models.Location?> GetLocationByQrAsync(string code, CancellationToken cancellationToken = default);
    Task<bool> CreateLocationAsync(TouristGuideApp.Models.Location location, CancellationToken cancellationToken = default);
    Task<bool> UpdateLocationAsync(TouristGuideApp.Models.Location location, CancellationToken cancellationToken = default);
    Task<bool> DeleteLocationAsync(int id, CancellationToken cancellationToken = default);

    // Localization support (multilingual)
    Task<List<LocalizationModel>> GetLocalizationsAsync(int locationId, CancellationToken cancellationToken = default);
    Task<LocalizationModel?> GetLocalizationAsync(int locationId, string languageCode, CancellationToken cancellationToken = default);
    Task<byte[]?> GetLocalizationAudioBytesAsync(int localizationId, CancellationToken cancellationToken = default);
    Task<int?> RecordPlayAsync(int localizationId, CancellationToken cancellationToken = default);

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
    private readonly Uri? _startupBaseAddress;
    private string? _authToken;
    private readonly object _baseAddressLock = new();
    private string? _lastBaseAddress;

    public ApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _startupBaseAddress = httpClient.BaseAddress;
        _lastBaseAddress = _startupBaseAddress?.AbsoluteUri;
        EnsureBaseAddressConfigured();
    }

    /// <summary>
    /// Set JWT token for authenticated requests
    /// </summary>
    public void SetAuthToken(string token)
    {
        EnsureBaseAddressConfigured();
        _authToken = token;
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Get POIs from API (converts Location to POI)
    /// </summary>
    public async Task<List<POI>> GetPOIsAsync(CancellationToken cancellationToken = default, string? query = null)
    {
        EnsureBaseAddressConfigured();
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var url = "api/locations";
            if (!string.IsNullOrWhiteSpace(query))
            {
                url += $"?query={Uri.EscapeDataString(query)}";
            }

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var locations = await response.Content.ReadFromJsonAsync<List<Models.Location>>(options, cancellationToken);

            if (locations == null) return new List<POI>();

            return locations.Select(MapLocationToPoi).ToList();
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
    public async Task SyncPOIsToLocalAsync(IDatabaseService databaseService, string? languageCode = null)
    {
        EnsureBaseAddressConfigured();
        try
        {
            var targetLanguage = string.IsNullOrWhiteSpace(languageCode)
                ? SupportedLanguages.Vietnamese
                : languageCode.Trim();

            var apiPois = await GetPOIsAsync();

            await databaseService.ClearAllPOIsAsync();

            if (apiPois != null && apiPois.Any())
            {
                foreach (var poi in apiPois)
                {
                    if (poi.ServerLocationId <= 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipping POI without ServerLocationId: {poi.Name}");
                        continue;
                    }

                    if (poi.Radius <= 0) poi.Radius = 100;

                    // Apply language pack (store localized text offline for QR/TTS)
                    if (!string.Equals(targetLanguage, SupportedLanguages.Vietnamese, StringComparison.OrdinalIgnoreCase))
                    {
                        var localization = await GetLocalizationAsync(poi.ServerLocationId, targetLanguage);
                        if (localization != null)
                        {
                            if (!string.IsNullOrWhiteSpace(localization.LocalizedName))
                            {
                                poi.Name = localization.LocalizedName.Trim();
                            }

                            if (!string.IsNullOrWhiteSpace(localization.LocalizedDescription))
                            {
                                poi.Description = localization.LocalizedDescription.Trim();
                                poi.LanguageCode = targetLanguage;
                            }
                        }
                    }

                    // Fallback: keep Vietnamese if we couldn't apply pack
                    if (string.IsNullOrWhiteSpace(poi.LanguageCode))
                    {
                        poi.LanguageCode = SupportedLanguages.Vietnamese;
                    }

                    if (string.IsNullOrWhiteSpace(poi.QrCodeData))
                    {
                        poi.QrCodeData = $"POI_{poi.ServerLocationId}";
                    }

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
        EnsureBaseAddressConfigured();
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
        EnsureBaseAddressConfigured();
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
    public async Task<byte[]?> GetLocalizationAudioBytesAsync(int localizationId, CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        try
        {
            var response = await _httpClient.GetAsync($"api/localizations/{localizationId}/audio", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsByteArrayAsync(cancellationToken);
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
    /// Record a play count for an audio localization
    /// </summary>
    public async Task<int?> RecordPlayAsync(int localizationId, CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        try
        {
            var response = await _httpClient.PostAsync($"api/localizations/{localizationId}/play", null, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(cancellationToken: cancellationToken);
                if (result.TryGetProperty("PlayCount", out var playCountProp))
                {
                    return playCountProp.GetInt32();
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Record play error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Ping API to check connectivity
    /// </summary>
    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        try
        {
            var response = await _httpClient.GetAsync("api/health", cancellationToken);
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
        EnsureBaseAddressConfigured();
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
        EnsureBaseAddressConfigured();
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

    public async Task<IReadOnlyList<Models.Location>> GetLocationsAsync(CancellationToken cancellationToken = default, string? query = null)
    {
        EnsureBaseAddressConfigured();
        var url = "api/locations";
        if (!string.IsNullOrWhiteSpace(query))
        {
            url += $"?query={Uri.EscapeDataString(query)}";
        }
        var result = await _httpClient.GetFromJsonAsync<List<Models.Location>>(url, cancellationToken);
        return result ?? new List<Models.Location>();
    }

    public Task<Models.Location?> GetLocationAsync(int id, CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        return _httpClient.GetFromJsonAsync<Models.Location>($"api/locations/{id}", cancellationToken);
    }

    public async Task<Models.Location?> GetLocationByQrAsync(string code, CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        if (string.IsNullOrWhiteSpace(code)) return null;

        try
        {
            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var url = $"api/locations/by-qr?code={Uri.EscapeDataString(code.Trim())}";
            return await _httpClient.GetFromJsonAsync<Models.Location>(url, options, cancellationToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetLocationByQr failed: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> CreateLocationAsync(Models.Location location, CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        NormalizeLocationForApi(location);
        var response = await _httpClient.PostAsJsonAsync("api/locations", location, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateLocationAsync(Models.Location location, CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        NormalizeLocationForApi(location);
        var response = await _httpClient.PutAsJsonAsync($"api/locations/{location.Id}", location, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteLocationAsync(int id, CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        var response = await _httpClient.DeleteAsync($"api/locations/{id}", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    // --- Tour Methods ---
    public async Task<List<Tour>> GetToursAsync(CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
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
        EnsureBaseAddressConfigured();
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

    private static void NormalizeLocationForApi(Models.Location location)
    {
        if (location.Id <= 0)
        {
            location.Id = 0;
        }

        if (string.IsNullOrWhiteSpace(location.QrCodeData))
        {
            location.QrCodeData = location.Id > 0 ? $"POI_{location.Id}" : string.Empty;
        }

        if (string.IsNullOrWhiteSpace(location.AudioUrl))
        {
            location.AudioUrl = string.Empty;
        }
    }

    private static POI MapLocationToPoi(Models.Location l)
    {
        var serverLocationId = l.Id > 0 ? l.Id : 0;
        return new POI
        {
            ServerLocationId = serverLocationId,
            QrCodeData = string.IsNullOrWhiteSpace(l.QrCodeData) ? (serverLocationId > 0 ? $"POI_{serverLocationId}" : null) : l.QrCodeData.Trim(),
            Name = l.Name ?? "Chưa đặt tên",
            Description = string.IsNullOrWhiteSpace(l.Description) ? "Không có mô tả" : l.Description,
            Latitude = l.Latitude,
            Longitude = l.Longitude,
            Radius = 30,
            Category = l.Category ?? string.Empty,
            PhoneNumber = l.PhoneNumber,
            Address = l.Address,
            ImageUrl = l.ImageUrl,
            Priority = l.Priority,
            LanguageCode = "vi-VN",
            AudioUrl = l.AudioUrl
        };
    }

    private void EnsureBaseAddressConfigured()
    {
        Uri? desiredBaseAddress = null;
        if (AppPreferences.TryGetApiBaseUrl(out var preferredApiBaseUrl) && preferredApiBaseUrl is not null)
        {
            desiredBaseAddress = preferredApiBaseUrl;
        }
        else if (_startupBaseAddress is not null)
        {
            desiredBaseAddress = _startupBaseAddress;
        }

        if (desiredBaseAddress is null)
        {
            return;
        }

        var normalized = desiredBaseAddress.AbsoluteUri;
        lock (_baseAddressLock)
        {
            if (string.Equals(_lastBaseAddress, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _httpClient.BaseAddress = new Uri(normalized);
            _lastBaseAddress = normalized;
        }
    }
}

