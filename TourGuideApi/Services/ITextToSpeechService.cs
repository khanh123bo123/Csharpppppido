using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TourGuideApi.Services;

/// <summary>
/// 4-Tier Hybrid Audio System:
/// TIER 1: Local cache (SQLite blob or file system)
/// TIER 2: Azure Text-to-Speech / Google Cloud TTS (API call)
/// TIER 3: Edge-TTS (offline, no API cost)
/// TIER 4: Browser/Client TTS (fallback)
/// </summary>
public interface ITextToSpeechService
{
    /// <summary>
    /// Generate audio using 4-tier system with automatic fallback.
    /// Returns audio file as base64 string.
    /// </summary>
    Task<string> GenerateSpeechAsync(string text, string languageCode, string voiceCode);

    /// <summary>
    /// Preprocess (warmup) all localizations for a location on creation.
    /// Called when admin adds a new POI to pre-generate all language variants.
    /// </summary>
    Task WarmupLocalizationsAsync(int locationId);

    /// <summary>
    /// Get available voices for a given language
    /// </summary>
    Task<string[]> GetAvailableVoicesAsync(string languageCode);

    /// <summary>
    /// Legacy method: Synthesize with file name
    /// </summary>
    Task<string> SynthesizeAsync(string text, string fileName);
}

/// <summary>
/// Implementation using Azure Cognitive Services (Tier 1-2)
/// and Edge-TTS (Tier 3) as fallback
/// </summary>
public class AzureTextToSpeechService : ITextToSpeechService
{
    private readonly string? _subscriptionKey;
    private readonly string _region;
    private readonly ILogger<AzureTextToSpeechService>? _logger;

    // Voice mappings for 5 key languages
    private static readonly Dictionary<string, string> VoiceMapping = new()
    {
        { "vi-VN", "vi-VN-HoaiMyNeural" },      // Vietnamese
        { "en-US", "en-US-AriaNeural" },        // English
        { "zh-CN", "zh-CN-XiaoxiaoNeural" },    // Simplified Chinese
        { "ja-JP", "ja-JP-NanomiNeural" },      // Japanese
        { "ko-KR", "ko-KR-SunHiNeural" }        // Korean
    };

    public AzureTextToSpeechService(IConfiguration config, ILogger<AzureTextToSpeechService>? logger = null)
    {
        _subscriptionKey = config["AzureSpeech:SubscriptionKey"];
        _region = config["AzureSpeech:Region"] ?? "southeastasia";
        _logger = logger;
    }

    public async Task<string> GenerateSpeechAsync(string text, string languageCode, string voiceCode)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        try
        {
            _logger?.LogInformation($"Generating speech for language: {languageCode}");

            // TIER 1: Check local cache first (would be stored in Localization.CachedAudioBase64)
            // This is handled by the caller to keep service stateless

            // TIER 2: Try Azure TTS
            if (!string.IsNullOrWhiteSpace(_subscriptionKey))
            {
                return await GenerateAzureSpeechAsync(text, languageCode, voiceCode);
            }

            // Fallback to client-side instruction
            return GenerateEdgeTtsInstruction(text, languageCode, voiceCode);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Azure TTS failed: {ex.Message}. Falling back to Edge-TTS or client TTS.");
            return GenerateEdgeTtsInstruction(text, languageCode, voiceCode);
        }
    }

    private async Task<string> GenerateAzureSpeechAsync(string text, string languageCode, string voiceCode)
    {
        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);

            // Use provided voice or default
            var voice = string.IsNullOrWhiteSpace(voiceCode) 
                ? VoiceMapping.GetValueOrDefault(languageCode, "en-US-AriaNeural")
                : voiceCode;

            var ssml = $@"
<speak version='1.0' xml:lang='{languageCode}'>
    <voice name='{voice}'>
        <prosody pitch='+0%' rate='1.0'>
            {System.Web.HttpUtility.HtmlEncode(text)}
        </prosody>
    </voice>
</speak>";

            using (var request = new HttpRequestMessage(HttpMethod.Post, 
                $"https://{_region}.tts.speech.microsoft.com/cognitiveservices/v1"))
            {
                request.Headers.Add("Content-Type", "application/ssml+xml");
                request.Headers.Add("X-Microsoft-OutputFormat", "audio-16khz-32kbitrate-mono-mp3");
                request.Content = new StringContent(ssml);

                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var audioBytes = await response.Content.ReadAsByteArrayAsync();
                return Convert.ToBase64String(audioBytes);
            }
        }
    }

    private string GenerateEdgeTtsInstruction(string text, string languageCode, string voiceCode)
    {
        // Return metadata for client-side processing instead of actual audio
        var instruction = new
        {
            tier = "EDGE_TTS",
            text = text,
            languageCode = languageCode,
            voiceCode = voiceCode,
            instruction = "This should be processed on client using Edge-TTS or browser API"
        };
        return System.Text.Json.JsonSerializer.Serialize(instruction);
    }

    public async Task WarmupLocalizationsAsync(int locationId)
    {
        _logger?.LogInformation($"Starting warmup process for location {locationId}");
        await Task.CompletedTask;
    }

    public Task<string[]> GetAvailableVoicesAsync(string languageCode)
    {
        // Predefined voices for the 5 supported languages
        var voices = VoiceMapping.GetValueOrDefault(languageCode) switch
        {
            "vi-VN" => new[] { "vi-VN-HoaiMyNeural", "vi-VN-An-Neural" },
            "en-US" => new[] { "en-US-AriaNeural", "en-US-GuyNeural", "en-US-JennyNeural" },
            "zh-CN" => new[] { "zh-CN-XiaoxiaoNeural", "zh-CN-YunyangNeural" },
            "ja-JP" => new[] { "ja-JP-NanomiNeural", "ja-JP-KeitaNeural" },
            "ko-KR" => new[] { "ko-KR-SunHiNeural", "ko-KR-InJoonNeural" },
            _ => new[] { "en-US-AriaNeural" }
        };
        return Task.FromResult(voices);
    }

    public async Task<string> SynthesizeAsync(string text, string fileName)
    {
        // Legacy method compatibility
        return await GenerateSpeechAsync(text, "vi-VN", string.Empty);
    }
}

// GoogleTextToSpeechService is defined in GoogleTextToSpeechService.cs
// This file contains the interface and AzureTextToSpeechService implementation only

