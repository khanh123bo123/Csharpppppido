using System.Net.Http.Json;
using TouristGuideWeb.Models;
using Microsoft.Extensions.Configuration;

namespace TouristGuideWeb.Services;

public class LocalizationApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _baseUrl;

    // Cache list mapping since locations might be fetched differently
    private readonly LocationApiService _locationApiService;

    public LocalizationApiService(IHttpClientFactory httpClientFactory, IConfiguration configuration, LocationApiService locationApiService)
    {
        _httpClientFactory = httpClientFactory;
        _baseUrl = configuration["ApiSettings:BaseUrl"] 
            ?? throw new InvalidOperationException("Missing configuration: ApiSettings:BaseUrl");
        _locationApiService = locationApiService;
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_baseUrl);
        return client;
    }

    public async Task<List<LocalizationDto>> GetLocalizationsByLocationAsync(int locationId, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        try 
        {
            var result = await client.GetFromJsonAsync<List<LocalizationDto>>($"api/localizations/by-location/{locationId}", cancellationToken);
            return result ?? new List<LocalizationDto>();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new List<LocalizationDto>();
        }
    }

    public async Task<LocalizationDto?> GetLocalizationAsync(int locationId, string languageCode, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        try
        {
            return await client.GetFromJsonAsync<LocalizationDto>($"api/localizations/{locationId}/{languageCode}", cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<LocalizationDto?> CreateOrUpdateAsync(CreateLocalizationRequest request, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("api/localizations", request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<LocalizationDto>(cancellationToken: cancellationToken);
        }
        return null;
    }

    public async Task<bool> DeleteAsync(int localizationId, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var response = await client.DeleteAsync($"api/localizations/{localizationId}", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<GenerateAudioResponse?> GenerateAudioAsync(int localizationId, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var request = new GenerateAudioRequest { LocalizationId = localizationId };
        var response = await client.PostAsJsonAsync("api/localizations/generate-audio", request, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
             return await response.Content.ReadFromJsonAsync<GenerateAudioResponse>(cancellationToken: cancellationToken);
        }
        return null;
    }

    // Helper to get ALL localizations across all locations (Since Backend API doesn't have a Get All endpoint)
    // We will fetch all locations, then query their localizations if needed. But this might be too heavy.
    // Instead, the Web Controller will only fetch localizations when a Location is selected.
}
