using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TourGuideApi.Services;

/// <summary>
/// Auto-translation using Google Gemini (Generative Language API) via API key.
/// Translates from Vietnamese (vi-VN) into multiple target languages.
///
/// Config:
/// - Translation:Provider = Gemini
/// - Gemini:ApiKey
/// - Gemini:Model (e.g., gemini-2.5-flash)
/// - Gemini:BaseUrl (optional, default https://generativelanguage.googleapis.com)
/// </summary>
public sealed class GeminiLocalizationTranslationService : ILocalizationTranslationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiLocalizationTranslationService> _logger;

    // Strict throttling for Free Tier (5 RPM limit = 1 request every 12 seconds)
    private static readonly SemaphoreSlim _rateLimiter = new SemaphoreSlim(1, 1);
    private static DateTime _lastRequestTime = DateTime.MinValue;
    private const int MinRequestIntervalMs = 13000; // 13 seconds buffer

    public GeminiLocalizationTranslationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GeminiLocalizationTranslationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Dictionary<string, LocalizedText>> TranslateFromVietnameseAsync(
        string vietnameseName,
        string vietnameseDescription,
        IReadOnlyCollection<string> targetLanguageCodes,
        CancellationToken cancellationToken = default)
    {
        vietnameseName = (vietnameseName ?? string.Empty).Trim();
        vietnameseDescription = (vietnameseDescription ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(vietnameseName) || string.IsNullOrWhiteSpace(vietnameseDescription))
        {
            throw new ArgumentException("Vietnamese name/description must not be empty.");
        }

        if (targetLanguageCodes is null || targetLanguageCodes.Count == 0)
        {
            return new Dictionary<string, LocalizedText>(StringComparer.OrdinalIgnoreCase);
        }

        var distinctTargets = targetLanguageCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (distinctTargets.Length == 0)
        {
            return new Dictionary<string, LocalizedText>(StringComparer.OrdinalIgnoreCase);
        }

        var apiKey = (_configuration["Gemini:ApiKey"] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Chưa cấu hình Gemini API key.");
        }

        var baseUrl = (_configuration["Gemini:BaseUrl"] ?? "https://generativelanguage.googleapis.com").Trim();
        var model = (_configuration["Gemini:Model"] ?? "gemini-2.5-flash").Trim();

        using var client = _httpClientFactory.CreateClient("GeminiTranslation");
        var output = new Dictionary<string, LocalizedText>(StringComparer.OrdinalIgnoreCase);

        foreach (var lang in distinctTargets)
        {
            output[lang] = await TranslateSingleLanguageWithRetryAsync(
                client,
                baseUrl,
                model,
                apiKey,
                vietnameseName,
                vietnameseDescription,
                lang,
                cancellationToken);
        }

        return output;
    }

    private async Task<LocalizedText> TranslateSingleLanguageWithRetryAsync(
        HttpClient client,
        string baseUrl,
        string model,
        string apiKey,
        string vietnameseName,
        string vietnameseDescription,
        string languageCode,
        CancellationToken cancellationToken)
    {
        const int MaxRetries = 5;
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await TranslateSingleLanguageOnceAsync(
                    client,
                    baseUrl,
                    model,
                    apiKey,
                    vietnameseName,
                    vietnameseDescription,
                    languageCode,
                    isRetry: attempt > 1,
                    cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                var isTransient = ex.Message.Contains("429") || ex.Message.Contains("503") || IsLikelyFormatOrValidationIssue(ex.Message);
                
                if (attempt == MaxRetries || !isTransient)
                {
                    _logger.LogWarning(ex, "Gemini translation failed after {Attempt} attempts for {LanguageCode}.", attempt, languageCode);
                    throw;
                }

                var delayMs = (int)Math.Pow(2, attempt) * 1000 + Random.Shared.Next(0, 1000);
                if (ex.Message.Contains("429")) delayMs = Math.Max(delayMs, 30000); 

                _logger.LogInformation("Gemini error (Attempt {Attempt}/{MaxRetries}): {Error}. Retrying in {DelayMs}ms...", attempt, MaxRetries, ex.Message, delayMs);
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        throw new InvalidOperationException($"Dịch {languageCode} thất bại sau {MaxRetries} lần thử.");
    }

    private async Task<LocalizedText> TranslateSingleLanguageOnceAsync(
        HttpClient client,
        string baseUrl,
        string model,
        string apiKey,
        string vietnameseName,
        string vietnameseDescription,
        string languageCode,
        bool isRetry,
        CancellationToken cancellationToken)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        try 
        {
            var now = DateTime.UtcNow;
            var elapsedMs = (now - _lastRequestTime).TotalMilliseconds;
            if (elapsedMs < MinRequestIntervalMs)
            {
                var delay = (int)(MinRequestIntervalMs - elapsedMs);
                _logger.LogInformation("Throttling Gemini request for {LanguageCode}. Sleeping for {DelayMs}ms.", languageCode, delay);
                await Task.Delay(delay, cancellationToken);
            }
            _lastRequestTime = DateTime.UtcNow;

            var endpoint = BuildEndpoint(baseUrl, model, apiKey);
            var systemPrompt = BuildSystemPrompt(languageCode);
            var userPrompt = BuildUserPromptSingle(vietnameseName, vietnameseDescription, languageCode, isRetry);

            var requestBody = new
            {
                contents = new object[]
                {
                    new
                    {
                        role = "user",
                        parts = new object[] { new { text = systemPrompt + "\n\n" + userPrompt } }
                    }
                },
                generationConfig = new
                {
                    temperature = isRetry ? 0.0 : 0.1,
                    topP = 0.9,
                    maxOutputTokens = 1024
                }
            };

            var response = await client.PostAsJsonAsync(endpoint, requestBody, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini translation failed. Status={Status}. Body={Body}", (int)response.StatusCode, body);
                throw new InvalidOperationException(BuildUserFacingGeminiErrorMessage((int)response.StatusCode, body));
            }

            var assistantText = ExtractGeminiText(body);
            if (string.IsNullOrWhiteSpace(assistantText)) throw new InvalidOperationException("Gemini response empty.");

            var json = TryExtractJsonObject(assistantText);
            var localized = JsonSerializer.Deserialize<LocalizedText>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (localized == null || string.IsNullOrWhiteSpace(localized.LocalizedName) || string.IsNullOrWhiteSpace(localized.LocalizedDescription))
            {
                throw new InvalidOperationException("Invalid JSON content from Gemini.");
            }

            return localized;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    private static Uri BuildEndpoint(string baseUrl, string model, string apiKey)
    {
        var builder = new UriBuilder(baseUrl.TrimEnd('/'))
        {
            Path = $"/v1/models/{model.Trim()}:generateContent",
            Query = $"key={Uri.EscapeDataString(apiKey)}"
        };
        return builder.Uri;
    }

    private static string BuildSystemPrompt(string languageCode)
    {
        return $"Bạn là dịch giả chuyên nghiệp. Dịch sát nghĩa nội dung du lịch sang {DescribeTargetLanguage(languageCode)}. TRẢ VỀ DUY NHẤT JSON: {{\"localizedName\":\"...\",\"localizedDescription\":\"...\"}}.";
    }

    private static string BuildUserPromptSingle(string vietnameseName, string vietnameseDescription, string languageCode, bool isRetry)
    {
        return $"Dịch: Name={vietnameseName}, Desc={vietnameseDescription}.";
    }

    private static string DescribeTargetLanguage(string languageCode)
    {
        return languageCode switch
        {
            "en-US" => "English",
            "zh-CN" => "Simplified Chinese",
            "ja-JP" => "Japanese",
            "ko-KR" => "Korean",
            _ => languageCode
        };
    }

    private static bool IsLikelyFormatOrValidationIssue(string message) => message.Contains("json") || message.Contains("schema");

    private static string ExtractGeminiText(string responseBodyJson)
    {
        using var doc = JsonDocument.Parse(responseBodyJson);
        return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
    }

    private static string TryExtractJsonObject(string text)
    {
        var first = text.IndexOf('{');
        var last = text.LastIndexOf('}');
        return (first >= 0 && last > first) ? text.Substring(first, last - first + 1) : text;
    }

    private static string BuildUserFacingGeminiErrorMessage(int code, string body) => $"Gemini Error {code}: {body}";
}
