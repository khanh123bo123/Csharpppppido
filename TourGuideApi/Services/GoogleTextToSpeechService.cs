using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TourGuideApi.Services;

/// <summary>
/// Google Cloud Text-to-Speech implementation (TIER 2 alternative)
/// </summary>
public class GoogleTextToSpeechService : ITextToSpeechService
{
    private readonly string? _apiKey;
    private readonly ILogger<GoogleTextToSpeechService>? _logger;

    private static readonly Dictionary<string, string> GoogleVoiceMapping = new()
    {
        { "vi-VN", "vi-VN-Neural2-A" },
        { "en-US", "en-US-Neural2-A" },
        { "zh-CN", "cmn-CN-Neural2-A" },
        { "ja-JP", "ja-JP-Neural2-B" },
        { "ko-KR", "ko-KR-Neural2-A" }
    };

    public GoogleTextToSpeechService(IConfiguration config, ILogger<GoogleTextToSpeechService>? logger = null)
    {
        _apiKey = config["GoogleCloud:TextToSpeechApiKey"];
        _logger = logger;
    }

    public async Task<string> GenerateSpeechAsync(string text, string languageCode, string voiceCode)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        try
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger?.LogWarning("Google TTS API key not configured");
                return string.Empty;
            }

            using (var client = new HttpClient())
            {
                var voice = string.IsNullOrWhiteSpace(voiceCode)
                    ? GoogleVoiceMapping.GetValueOrDefault(languageCode, "en-US-Neural2-A")
                    : voiceCode;

                var requestBody = new
                {
                    input = new { text = text },
                    voice = new { languageCode = languageCode, name = voice },
                    audioConfig = new { audioEncoding = "MP3" }
                };

                var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await client.PostAsync(
                    $"https://texttospeech.googleapis.com/v1/text:synthesize?key={_apiKey}",
                    content);

                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync();
                
                // Extract audio content from response
                var jsonDoc = System.Text.Json.JsonDocument.Parse(responseBody);
                var audioContent = jsonDoc.RootElement
                    .GetProperty("audioContent")
                    .GetString() ?? string.Empty;

                return audioContent;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Google TTS failed: {ex.Message}");
            return string.Empty;
        }
    }

    public async Task WarmupLocalizationsAsync(int locationId)
    {
        _logger?.LogInformation($"Google TTS warmup for location {locationId}");
        await Task.CompletedTask;
    }

    public Task<string[]> GetAvailableVoicesAsync(string languageCode)
    {
        var voices = GoogleVoiceMapping.GetValueOrDefault(languageCode) switch
        {
            "vi-VN" => new[] { "vi-VN-Neural2-A" },
            "en-US" => new[] { "en-US-Neural2-A", "en-US-Neural2-C", "en-US-Neural2-E" },
            "zh-CN" => new[] { "cmn-CN-Neural2-A" },
            "ja-JP" => new[] { "ja-JP-Neural2-B" },
            "ko-KR" => new[] { "ko-KR-Neural2-A" },
            _ => new[] { "en-US-Neural2-A" }
        };
        return Task.FromResult(voices);
    }

    public async Task<string> SynthesizeAsync(string text, string fileName)
    {
        // Legacy method compatibility
        return await GenerateSpeechAsync(text, "vi-VN", string.Empty);
    }
}
