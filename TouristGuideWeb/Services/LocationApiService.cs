using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TouristGuideWeb.Models;

namespace TouristGuideWeb.Services;

public class LocationApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _baseUrl;

    public LocationApiService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _baseUrl = configuration["ApiSettings:BaseUrl"]
            ?? throw new InvalidOperationException("Missing configuration: ApiSettings:BaseUrl");
    }

    public async Task<List<Location>> GetLocationsAsync(CancellationToken cancellationToken = default, string? query = null, string? category = null)
    {
        using var client = CreateClient();
        try
        {
            var url = "api/locations";
            if (!string.IsNullOrWhiteSpace(query))
            {
                url += $"?query={Uri.EscapeDataString(query)}";
            }
            if (!string.IsNullOrWhiteSpace(category))
            {
                url += url.Contains('?') ? $"&category={Uri.EscapeDataString(category)}" : $"?category={Uri.EscapeDataString(category)}";
            }

            var result = await client.GetFromJsonAsync<List<Location>>(url, cancellationToken);
            return result ?? new List<Location>();
        }
        catch (OperationCanceledException)
        {
            return new List<Location>();
        }
        catch (HttpRequestException)
        {
            return new List<Location>();
        }
    }

    public Task<List<Location>> GetAllAsync(CancellationToken cancellationToken = default, string? query = null, string? category = null)
    {
        return GetLocationsAsync(cancellationToken, query, category);
    }

    public async Task<List<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        try
        {
            var result = await client.GetFromJsonAsync<List<string>>("api/locations/categories", cancellationToken);
            return result ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    public async Task<Location?> GetLocationByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        try
        {
            return await client.GetFromJsonAsync<Location>($"api/locations/{id}", cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<(Location? Location, string? ErrorMessage)> CreateLocationAsync(Location location, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        try
        {
            var response = await client.PostAsJsonAsync("api/locations", location, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return (null, await ReadErrorMessageAsync(response, cancellationToken));
            }

            var created = await response.Content.ReadFromJsonAsync<Location>(cancellationToken: cancellationToken);
            return (created, null);
        }
        catch (OperationCanceledException)
        {
            return (null, "Không thể kết nối API (timeout / request bị huỷ). Vui lòng thử lại.");
        }
        catch (HttpRequestException)
        {
            return (null, "Không thể kết nối API. Vui lòng kiểm tra TourGuideApi đang chạy.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateLocationAsync(int id, Location location, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        try
        {
            var response = await client.PutAsJsonAsync($"api/locations/{id}", location, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            return (false, await ReadErrorMessageAsync(response, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            return (false, "Không thể kết nối API (timeout / request bị huỷ). Vui lòng thử lại.");
        }
        catch (HttpRequestException)
        {
            return (false, "Không thể kết nối API. Vui lòng kiểm tra TourGuideApi đang chạy.");
        }
    }

    public async Task<bool> DeleteLocationAsync(int id, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        try
        {
            var response = await client.DeleteAsync($"api/locations/{id}", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task<List<ScanLogDto>> GetScanLogsAsync(CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        try
        {
            var result = await client.GetFromJsonAsync<List<ScanLogDto>>("api/locations/scan-logs", cancellationToken);
            return result ?? new List<ScanLogDto>();
        }
        catch
        {
            return new List<ScanLogDto>();
        }
    }

    public async Task<List<RatingDto>> GetRecentRatingsAsync(CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        try
        {
            var result = await client.GetFromJsonAsync<List<RatingDto>>("api/locations/recent-ratings", cancellationToken);
            return result ?? new List<RatingDto>();
        }
        catch
        {
            return new List<RatingDto>();
        }
    }

    public async Task<int> GetOnlineCountAsync(CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        try
        {
            var response = await client.GetAsync("api/status/online-count", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
                if (content.TryGetProperty("onlineCount", out var countProp) && countProp.ValueKind == JsonValueKind.Number)
                {
                    return countProp.GetInt32();
                }
            }
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    public async Task<int> GetListenStatsAsync(CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        try
        {
            var response = await client.GetAsync("api/status/listen-stats", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
                if (content.TryGetProperty("totalListens", out var countProp) && countProp.ValueKind == JsonValueKind.Number)
                {
                    return countProp.GetInt32();
                }
            }
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    public async Task<bool> ReprocessTranslationsAsync(CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        try
        {
            var response = await client.PostAsync("api/locations/reprocess-all", null, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_baseUrl);
        return client;
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return $"API call failed with status code {(int)response.StatusCode}.";
        }

        try
        {
            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;

            if (root.TryGetProperty("title", out var titleProperty) && titleProperty.ValueKind == JsonValueKind.String)
            {
                var title = titleProperty.GetString();
                if (!string.IsNullOrWhiteSpace(title))
                {
                    return title;
                }
            }

            if (root.TryGetProperty("message", out var messageProperty) && messageProperty.ValueKind == JsonValueKind.String)
            {
                var message = messageProperty.GetString();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return message;
                }
            }
        }
        catch (JsonException)
        {
            // Fall back to plain response text.
        }

        return responseText;
    }
}