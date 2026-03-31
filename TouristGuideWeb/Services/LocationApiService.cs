using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using TouristGuideWeb.Models;

namespace TouristGuideWeb.Services;

public class LocationApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _baseUrl;

    public LocationApiService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _baseUrl = configuration["ApiSettings:BaseUrl"]
            ?? throw new InvalidOperationException("Missing configuration: ApiSettings:BaseUrl");
    }

    public async Task<List<Location>> GetLocationsAsync(CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var result = await client.GetFromJsonAsync<List<Location>>("api/locations", cancellationToken);
        return result ?? new List<Location>();
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

    public async Task<Location?> CreateLocationAsync(Location location, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("api/locations", location, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<Location>(cancellationToken: cancellationToken);
    }

    public async Task<bool> UpdateLocationAsync(int id, Location location, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var response = await client.PutAsJsonAsync($"api/locations/{id}", location, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteLocationAsync(int id, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var response = await client.DeleteAsync($"api/locations/{id}", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_baseUrl);
        return client;
    }
}