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

    public async Task<List<Location>> GetLocationsAsync(CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(LocationsListCacheKey, out List<Location>? cachedLocations) && cachedLocations is not null)
        {
            return cachedLocations;
        }

        using var client = CreateClient();
        var result = await client.GetFromJsonAsync<List<Location>>("api/locations", cancellationToken);

        var locations = result ?? new List<Location>();
        _memoryCache.Set(LocationsListCacheKey, locations, TimeSpan.FromMinutes(5));

        return locations;
    }

    public Task<List<Location>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return GetLocationsAsync(cancellationToken);
    }

    public async Task<Location?> GetLocationByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<Location>($"api/locations/{id}", cancellationToken);
    }

    public async Task<(Location? Location, string? ErrorMessage)> CreateLocationAsync(Location location, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("api/locations", location, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return (null, await ReadErrorMessageAsync(response, cancellationToken));
        }

        var created = await response.Content.ReadFromJsonAsync<Location>(cancellationToken: cancellationToken);
        _memoryCache.Remove(LocationsListCacheKey);
        return (created, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateLocationAsync(int id, Location location, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var response = await client.PutAsJsonAsync($"api/locations/{id}", location, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            _memoryCache.Remove(LocationsListCacheKey);
            return (true, null);
        }

        return (false, await ReadErrorMessageAsync(response, cancellationToken));
    }

    public async Task<bool> DeleteLocationAsync(int id, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var response = await client.DeleteAsync($"api/locations/{id}", cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            _memoryCache.Remove(LocationsListCacheKey);
        }

        return response.IsSuccessStatusCode;
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