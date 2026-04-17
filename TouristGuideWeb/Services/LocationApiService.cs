using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using TouristGuideWeb.Models;

namespace TouristGuideWeb.Services;

public class LocationApiService
{
    private const string LocationsListCacheKey = "LocationsList";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly string _baseUrl;

    public LocationApiService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _memoryCache = memoryCache;
        _baseUrl = configuration["ApiSettings:BaseUrl"]
            ?? throw new InvalidOperationException("Missing configuration: ApiSettings:BaseUrl");
    }

    public async Task<List<Location>> GetLocationsAsync(CancellationToken cancellationToken = default, string? query = null, string? category = null)
    {
        // Don't use cache if searching or filtering
        if (string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(category))
        {
            if (_memoryCache.TryGetValue(LocationsListCacheKey, out List<Location>? cachedLocations) && cachedLocations is not null)
            {
                return cachedLocations;
            }
        }

        using var client = CreateClient();
        try
        {
            var url = "api/locations";
            if (!string.IsNullOrWhiteSpace(query))
            {
                url += url.Contains('?') ? $"&query={Uri.EscapeDataString(query)}" : $"?query={Uri.EscapeDataString(query)}";
            }
            if (!string.IsNullOrWhiteSpace(category))
            {
                url += url.Contains('?') ? $"&category={Uri.EscapeDataString(category)}" : $"?category={Uri.EscapeDataString(category)}";
            }

            var result = await client.GetFromJsonAsync<List<Location>>(url, cancellationToken);
            var locations = result ?? new List<Location>();

            if (string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(category))
            {
                _memoryCache.Set(LocationsListCacheKey, locations, TimeSpan.FromMinutes(5));
            }

            return locations;
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
            _memoryCache.Remove(LocationsListCacheKey);
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
                _memoryCache.Remove(LocationsListCacheKey);
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
            if (response.IsSuccessStatusCode)
            {
                _memoryCache.Remove(LocationsListCacheKey);
            }

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