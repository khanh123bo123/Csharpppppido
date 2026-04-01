using System.Net.Http.Json;
using TouristGuideApp.Models;

namespace TouristGuideApp.Services;

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;

    public ApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<Location>> GetLocationsAsync(CancellationToken cancellationToken = default)
    {
        var result = await _httpClient.GetFromJsonAsync<List<Location>>("api/locations", cancellationToken);
        return result ?? new List<Location>();
    }

    public Task<Location?> GetLocationByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return _httpClient.GetFromJsonAsync<Location>($"api/locations/{id}", cancellationToken);
    }

    public async Task<bool> CreateLocationAsync(Location location, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/locations", location, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateLocationAsync(Location location, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/locations/{location.Id}", location, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteLocationAsync(int id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/locations/{id}", cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
