using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TourGuideApi.Services;

public class GoogleFreeTextToSpeechService : ITextToSpeechService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GoogleFreeTextToSpeechService> _logger;

    public GoogleFreeTextToSpeechService(
        IHttpClientFactory httpClientFactory,
        ILogger<GoogleFreeTextToSpeechService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> GenerateSpeechAsync(string text, string languageCode, string voiceCode)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var googleLangCode = MapToGoogleLangCode(languageCode);
        
        try
        {
            using var client = _httpClientFactory.CreateClient("GoogleFreeTts");
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            
            // Chunk the text to avoid 200 char limit.
            var chunks = ChunkText(text, 150);
            using var ms = new MemoryStream();

            foreach (var chunk in chunks)
            {
                var url = $"https://translate.google.com/translate_tts?ie=UTF-8&q={Uri.EscapeDataString(chunk)}&tl={googleLangCode}&client=tw-ob";
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var audioBytes = await response.Content.ReadAsByteArrayAsync();
                await ms.WriteAsync(audioBytes, 0, audioBytes.Length);
                await Task.Delay(100); // Be polite to the free API
            }

            return Convert.ToBase64String(ms.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Free TTS generation failed for {LangCode}", languageCode);
            return string.Empty;
        }
    }

    private System.Collections.Generic.List<string> ChunkText(string text, int maxChunkSize)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var chunks = new System.Collections.Generic.List<string>();
        var currentChunk = string.Empty;

        foreach (var word in words)
        {
            if (currentChunk.Length + word.Length + 1 > maxChunkSize)
            {
                if (!string.IsNullOrWhiteSpace(currentChunk))
                {
                    chunks.Add(currentChunk.Trim());
                    currentChunk = string.Empty;
                }
            }
            currentChunk += word + " ";
        }

        if (!string.IsNullOrWhiteSpace(currentChunk))
        {
            chunks.Add(currentChunk.Trim());
        }

        return chunks;
    }

    public Task WarmupLocalizationsAsync(int locationId)
    {
        return Task.CompletedTask;
    }

    public Task<string[]> GetAvailableVoicesAsync(string languageCode)
    {
        return Task.FromResult(new[] { "Google-TTS-Default" });
    }

    public Task<string> SynthesizeAsync(string text, string fileName)
    {
        return GenerateSpeechAsync(text, "vi-VN", string.Empty);
    }

    private string MapToGoogleLangCode(string standardCode)
    {
        return standardCode switch
        {
            "en-US" => "en",
            "zh-CN" => "zh",
            "ja-JP" => "ja",
            "ko-KR" => "ko",
            _ => standardCode.Split('-')[0]
        };
    }
}
