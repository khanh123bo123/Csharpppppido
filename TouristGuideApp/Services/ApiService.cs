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

    // Connectivity check
    Task<bool> PingAsync(CancellationToken cancellationToken = default);

    // Authentication
    Task<string?> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<(string? Token, string? ErrorMessage)> LoginWithDetailsAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<bool> VerifyTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage)> RegisterAsync(string email, string password, string fullName, CancellationToken cancellationToken = default);
    
    // Tours
    Task<List<Tour>> GetToursAsync(string? languageCode = null, CancellationToken cancellationToken = default);
    Task<List<TourLocation>> GetTourLocationsAsync(int tourId, CancellationToken cancellationToken = default);
    
    Task<bool> SubmitRatingAsync(int locationId, int stars, string? deviceId = null, string? userEmail = null);
    Task SendHeartbeatAsync(string? deviceId = null, CancellationToken cancellationToken = default);
    Task<bool> LogListenAsync(int locationId, string languageCode, string? deviceId = null, CancellationToken cancellationToken = default);
    void SetBaseAddress(string baseAddress);

    string BaseAddress { get; }
}

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly System.Text.Json.JsonSerializerOptions _jsonOptions;
    private string? _authToken;

    public string BaseAddress => _httpClient.BaseAddress?.ToString() ?? "http://172.20.10.2:5214/";

    public ApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        var customApiBaseUrl = AppPreferences.GetApiBaseUrl();

        // During development/testing, force the known working IP if nothing valid is set
        if (string.IsNullOrWhiteSpace(customApiBaseUrl) || customApiBaseUrl.Contains("localhost") || customApiBaseUrl.Contains("127.0.0.1"))
        {
            customApiBaseUrl = "http://172.20.10.2:5214/";
        }

        if (!string.IsNullOrWhiteSpace(customApiBaseUrl) &&
            Uri.TryCreate(customApiBaseUrl, UriKind.Absolute, out var customUri))
        {
            _httpClient.BaseAddress = EnsureTrailingSlash(customUri);
        }

        _jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public void SetBaseAddress(string baseAddress)
    {
        if (string.IsNullOrWhiteSpace(baseAddress))
        {
            throw new ArgumentException("API base address is empty.", nameof(baseAddress));
        }

        if (!Uri.TryCreate(baseAddress.Trim(), UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("API base address is invalid.", nameof(baseAddress));
        }

        _httpClient.BaseAddress = EnsureTrailingSlash(uri);
        AppPreferences.SetApiBaseUrl(_httpClient.BaseAddress.ToString());
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
    public async Task<List<POI>> GetPOIsAsync(CancellationToken cancellationToken = default, string? query = null)
    {
        try
        {
            var options = _jsonOptions;

            var url = "api/locations";
            if (!string.IsNullOrWhiteSpace(query))
            {
                url += $"?query={Uri.EscapeDataString(query)}";
            }

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var locations = await response.Content.ReadFromJsonAsync<List<Models.Location>>(options, cancellationToken);

            if (locations == null) return new List<POI>();

            return locations.Select(l => new POI
            {
                ServerLocationId = l.Id,
                QrCodeData = string.IsNullOrWhiteSpace(l.QrCodeData) ? null : l.QrCodeData.Trim(),
                Name = l.Name ?? "Chưa đặt tên",
                Description = l.Description ?? "Không có mô tả",
                Latitude = l.Latitude,
                Longitude = l.Longitude,
                Radius = 30,
                Category = string.IsNullOrWhiteSpace(l.Category) ? "Chưa phân loại" : l.Category,
                PhoneNumber = l.PhoneNumber,
                Address = l.Address,
                ImageUrl = EnsureAbsoluteUrl(l.ImageUrl),
                LanguageCode = "vi-VN",
                AudioUrl = EnsureAbsoluteUrl(l.AudioUrl),
                AverageRating = l.AverageRating,
                RatingCount = l.RatingCount
            }).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"API ERROR: {ex.Message}");
            throw;
        }
    }

    private string EnsureAbsoluteUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
        {
            var host = absoluteUri.Host.ToLowerInvariant();
            var isLoopbackHost = host is "localhost" or "127.0.0.1" or "::1" or "10.0.2.2";
            if (!isLoopbackHost)
            {
                return absoluteUri.AbsoluteUri;
            }

            var baseUri = _httpClient.BaseAddress;
            if (baseUri == null)
            {
                return absoluteUri.AbsoluteUri;
            }

            var builder = new UriBuilder(absoluteUri)
            {
                Scheme = baseUri.Scheme,
                Host = baseUri.Host,
                Port = baseUri.IsDefaultPort ? -1 : baseUri.Port
            };

            return builder.Uri.AbsoluteUri;
        }
        
        var baseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/');
        return $"{baseUrl}/{url.TrimStart('/')}";
    }

    /// <summary>
    /// Sync POIs to local database (offline-first)
    /// </summary>
    public async Task SyncPOIsToLocalAsync(IDatabaseService databaseService, string? languageCode = null)
    {
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
            var localizations = await _httpClient.GetFromJsonAsync<List<LocalizationModel>>(
                $"api/localizations/by-location/{locationId}",
                _jsonOptions,
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
            return await _httpClient.GetFromJsonAsync<LocalizationModel>(
                $"api/localizations/{locationId}/{languageCode}",
                _jsonOptions,
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
    /// Ping API to check connectivity
    /// </summary>
    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        foreach (var candidate in BuildCandidateBaseAddresses())
        {
            try
            {
                _httpClient.BaseAddress = candidate;
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(4));
                var response = await _httpClient.GetAsync("api/health", timeoutCts.Token);
                if (response.IsSuccessStatusCode)
                {
                    AppPreferences.SetApiBaseUrl(candidate.AbsoluteUri);
                    return true;
                }
            }
            catch
            {
                // Try next candidate base URL
            }
        }

        return false;
    }

    /// <summary>
    /// Admin or User login to get JWT token
    /// </summary>
    public async Task<string?> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var result = await LoginWithDetailsAsync(email, password, cancellationToken);
        return result.Token;
    }

    public async Task<(string? Token, string? ErrorMessage)> LoginWithDetailsAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email?.Trim().ToLowerInvariant() ?? "";
        var request = new { email = normalizedEmail, password };

        // 1. Try existing working BaseAddress first (fastest)
        if (_httpClient.BaseAddress != null)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/auth/login", request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LoginResponseModel>(_jsonOptions, cancellationToken);
                    if (result != null && !string.IsNullOrEmpty(result.Token))
                    {
                        SetAuthToken(result.Token);
                        return (result.Token, null);
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    return (null, string.IsNullOrWhiteSpace(body) ? "Sai email hoặc mật khẩu." : body.Trim().Trim('"'));
                }
            }
            catch { /* Fallback to candidate loop */ }
        }

        // 2. Candidate loop (discovery mode)
        foreach (var candidate in BuildCandidateBaseAddresses())
        {
            if (candidate == _httpClient.BaseAddress) continue; // Already tried

            try
            {
                using var client = new HttpClient();
                client.BaseAddress = candidate;
                client.Timeout = TimeSpan.FromSeconds(5);

                var response = await client.PostAsJsonAsync("api/auth/login", request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LoginResponseModel>(_jsonOptions, cancellationToken);
                    if (result != null && !string.IsNullOrEmpty(result.Token))
                    {
                        AppPreferences.SetApiBaseUrl(candidate.ToString());
                        SetAuthToken(result.Token);
                        return (result.Token, null);
                    }
                }
            }
            catch { /* Try next candidate */ }
        }

        return (null, "Không thể kết nối máy chủ đăng nhập. Vui lòng kiểm tra mạng.");
    }

    /// <summary>
    /// Register a new user account - returns (success, errorMessage)
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> RegisterAsync(string email, string password, string fullName, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email?.Trim().ToLowerInvariant() ?? "";
        var request = new { email = normalizedEmail, password, fullName = fullName?.Trim() ?? "", role = "User" };

        // 1. Try existing working BaseAddress first
        if (_httpClient.BaseAddress != null)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/auth/register", request, cancellationToken);
                if (response.IsSuccessStatusCode) return (true, null);
                
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return (false, string.IsNullOrWhiteSpace(body) ? "Đăng ký thất bại." : body.Trim().Trim('"'));
            }
            catch { /* Fallback */ }
        }

        // 2. Candidate loop
        foreach (var candidate in BuildCandidateBaseAddresses())
        {
            if (candidate == _httpClient.BaseAddress) continue;

            try
            {
                using var client = new HttpClient();
                client.BaseAddress = candidate;
                client.Timeout = TimeSpan.FromSeconds(5);

                var response = await client.PostAsJsonAsync("api/auth/register", request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    AppPreferences.SetApiBaseUrl(candidate.AbsoluteUri);
                    return (true, null);
                }
            }
            catch { }
        }

        return (false, "Không thể kết nối đến máy chủ để đăng ký.");
    }

    private class LoginResponseModel
    {
        public string Token { get; set; } = string.Empty;
    }

    private class ErrorResponse
    {
        public string? Message { get; set; }
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

    public async Task<IReadOnlyList<Models.Location>> GetLocationsAsync(CancellationToken cancellationToken = default, string? query = null)
    {
        var url = "api/locations";
        if (!string.IsNullOrWhiteSpace(query))
        {
            url += $"?query={Uri.EscapeDataString(query)}";
        }
        var result = await _httpClient.GetFromJsonAsync<List<Models.Location>>(url, _jsonOptions, cancellationToken);
        return result ?? new List<Models.Location>();
    }

    public Task<Models.Location?> GetLocationAsync(int id, CancellationToken cancellationToken = default)
    {
        return _httpClient.GetFromJsonAsync<Models.Location>($"api/locations/{id}", _jsonOptions, cancellationToken);
    }

    public async Task<Models.Location?> GetLocationByQrAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        try
        {
            var url = $"api/locations/by-qr?code={Uri.EscapeDataString(code.Trim())}";
            return await _httpClient.GetFromJsonAsync<Models.Location>(url, _jsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetLocationByQr failed: {ex.Message}");
            return null;
        }
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
    public async Task<List<Tour>> GetToursAsync(string? languageCode = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = "api/tours";
            if (!string.IsNullOrWhiteSpace(languageCode))
            {
                url += $"?languageCode={Uri.EscapeDataString(languageCode)}";
            }

            var result = await _httpClient.GetFromJsonAsync<List<Tour>>(url, _jsonOptions, cancellationToken);
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
            var result = await _httpClient.GetFromJsonAsync<List<TourLocation>>($"api/tours/{tourId}/locations", _jsonOptions, cancellationToken);
            return result ?? new List<TourLocation>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to fetch tour locations: {ex.Message}");
            return new List<TourLocation>();
        }
    }

    public async Task SendHeartbeatAsync(string? deviceId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var id = deviceId ?? Microsoft.Maui.Devices.DeviceInfo.Current.Name ?? "UnknownDevice";
            await _httpClient.PostAsync($"api/status/heartbeat?deviceId={Uri.EscapeDataString(id)}", null, cancellationToken);
        }
        catch { /* Ignore heartbeat errors to not disrupt UX */ }
    }

    public async Task<bool> SubmitRatingAsync(int locationId, int stars, string? deviceId = null, string? userEmail = null)
    {
        try
        {
            var url = $"api/locations/{locationId}/rate?stars={stars}";
            if (!string.IsNullOrWhiteSpace(deviceId)) url += $"&deviceId={Uri.EscapeDataString(deviceId)}";
            if (!string.IsNullOrWhiteSpace(userEmail)) url += $"&userEmail={Uri.EscapeDataString(userEmail)}";

            var response = await _httpClient.PostAsync(url, null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Rating submission failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> LogListenAsync(int locationId, string languageCode, string? deviceId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var id = deviceId ?? Microsoft.Maui.Devices.DeviceInfo.Current.Name ?? "UnknownDevice";
            var url = $"api/status/log-listen?locationId={locationId}&languageCode={Uri.EscapeDataString(languageCode)}&deviceId={Uri.EscapeDataString(id)}";
            var response = await _httpClient.PostAsync(url, null, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LogListen failed: {ex.Message}");
            return false;
        }
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        var absoluteUri = uri.AbsoluteUri;
        return absoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri(absoluteUri + "/");
    }

    private List<Uri> BuildCandidateBaseAddresses()
    {
        var candidates = new List<string>
        {
            BaseAddress
        };

        var custom = AppPreferences.GetApiBaseUrl();
        if (!string.IsNullOrWhiteSpace(custom))
        {
            candidates.Add(custom);
        }

        // Common local/dev candidates. The first reachable one will be persisted.
        candidates.Add("http://172.20.10.2:5214/");
        candidates.Add("http://10.0.2.2:5214/");
        candidates.Add("http://127.0.0.1:5214/");
        candidates.Add("http://localhost:5214/");

        var result = new List<Uri>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in candidates)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri)) continue;
            var normalized = EnsureTrailingSlash(uri).AbsoluteUri;
            if (seen.Add(normalized))
            {
                result.Add(new Uri(normalized));
            }
        }

        return result;
    }
}

