using System.Net.Http.Json;
using TouristGuideWeb.Models;

namespace TouristGuideWeb.Services;

public sealed class TtsSettingsApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _baseUrl;

    public TtsSettingsApiService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
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

    public async Task<EdgeTtsSettingsDto?> GetEdgeTtsSettingsAsync(CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        try
        {
            return await client.GetFromJsonAsync<EdgeTtsSettingsDto>("api/settings/edge-tts", cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    public async Task<EdgeTtsSettingsDto?> UpdateSpeechRateAsync(double speechRate, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        try
        {
            var request = new UpdateEdgeTtsSettingsRequest { SpeechRate = speechRate };
            using var response = await client.PostAsJsonAsync("api/settings/edge-tts", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<EdgeTtsSettingsDto>(cancellationToken: cancellationToken);
        }
        catch
        {
            return null;
        }
    }
}
