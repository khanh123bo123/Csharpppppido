using System.Net.Http.Json;
using TouristGuideWeb.Models;
using Microsoft.Extensions.Configuration;

namespace TouristGuideWeb.Services;

public class TourApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _baseUrl;

    public TourApiService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _baseUrl = configuration["ApiSettings:BaseUrl"] 
            ?? throw new InvalidOperationException("Missing configuration: ApiSettings:BaseUrl");
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_baseUrl);
        return client;
    }

    public async Task<List<Tour>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<Tour>>("api/Tours", cancellationToken) ?? new List<Tour>();
    }

    public async Task<Tour?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        try 
        {
            return await client.GetFromJsonAsync<Tour>($"api/Tours/{id}", cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<TourLocation>> GetLocationsAsync(int tourId, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<TourLocation>>($"api/Tours/{tourId}/locations", cancellationToken) ?? new List<TourLocation>();
    }

    public async Task<Tour?> CreateAsync(Tour tour, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("api/Tours", tour, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<Tour>(cancellationToken: cancellationToken);
        }
        return null;
    }

    public async Task<bool> UpdateAsync(int id, Tour tour, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var response = await client.PutAsJsonAsync($"api/Tours/{id}", tour, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var response = await client.DeleteAsync($"api/Tours/{id}", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> AddLocationAsync(int tourId, int locationId, int orderIndex, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var response = await client.PostAsync($"api/Tours/{tourId}/locations/{locationId}?orderIndex={orderIndex}", null, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RemoveLocationAsync(int tourId, int locationId, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var response = await client.DeleteAsync($"api/Tours/{tourId}/locations/{locationId}", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ReorderAsync(int tourId, List<ReorderItemDto> items, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var response = await client.PutAsJsonAsync($"api/Tours/{tourId}/locations/reorder", items, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}

public class ReorderItemDto
{
    public int LocationId { get; set; }
    public int OrderIndex { get; set; }
}
