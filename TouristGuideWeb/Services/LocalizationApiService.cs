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
        catch (OperationCanceledException)
        {
            // Covers TaskCanceledException (timeout) and request-abort cancellations.
            return new List<LocalizationDto>();
        }
        catch (HttpRequestException)
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
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<LocalizationDto?> CreateOrUpdateAsync(CreateLocalizationRequest request, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        try
        {
            var response = await client.PostAsJsonAsync("api/localizations", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<LocalizationDto>(cancellationToken: cancellationToken);
            }
            return null;
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

    public async Task<bool> DeleteAsync(int localizationId, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        try
        {
            var response = await client.DeleteAsync($"api/localizations/{localizationId}", cancellationToken);
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

    public async Task<bool> DeleteAudioAsync(int localizationId, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        try
        {
            var response = await client.DeleteAsync($"api/localizations/{localizationId}/audio", cancellationToken);
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

    public async Task<GenerateAudioResponse?> GenerateAudioAsync(int localizationId, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        try
        {
            var request = new GenerateAudioRequest { LocalizationId = localizationId };
            var response = await client.PostAsJsonAsync("api/localizations/generate-audio", request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<GenerateAudioResponse>(cancellationToken: cancellationToken);
            }
            return null;
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

    public async Task<GenerateLocalizationPackResponse?> GenerateLocalizationPackAsync(
        GenerateLocalizationPackRequest request,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        try
        {
            var response = await client.PostAsJsonAsync("api/localizations/generate-pack", request, cancellationToken);
            return await response.Content.ReadFromJsonAsync<GenerateLocalizationPackResponse>(cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<byte[]?> GetAudioBytesAsync(int localizationId, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();

        try
        {
            using var response = await client.GetAsync($"api/localizations/{localizationId}/audio", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    // Helper to get ALL localizations across all locations (Since Backend API doesn't have a Get All endpoint)
    // We will fetch all locations, then query their localizations if needed. But this might be too heavy.
    // Instead, the Web Controller will only fetch localizations when a Location is selected.
}
