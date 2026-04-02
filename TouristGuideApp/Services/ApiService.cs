using System.Net.Http.Json;
namespace TouristGuideApp.Services;

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;

    public ApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<TouristGuideApp.Models.Location>> GetLocationsAsync(CancellationToken cancellationToken = default)
    {
        var result = await _httpClient.GetFromJsonAsync<List<TouristGuideApp.Models.Location>>("api/locations", cancellationToken);
        return result ?? new List<TouristGuideApp.Models.Location>();
    }

    public Task<TouristGuideApp.Models.Location> GetLocationByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return _httpClient.GetFromJsonAsync<TouristGuideApp.Models.Location>($"api/locations/{id}", cancellationToken);
    }

    public async Task<bool> CreateLocationAsync(TouristGuideApp.Models.Location location, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/locations", location, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateLocationAsync(TouristGuideApp.Models.Location location, CancellationToken cancellationToken = default)
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
