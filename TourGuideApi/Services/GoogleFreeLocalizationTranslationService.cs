using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TourGuideApi.Services;

public class GoogleFreeLocalizationTranslationService : ILocalizationTranslationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GoogleFreeLocalizationTranslationService> _logger;

    public GoogleFreeLocalizationTranslationService(
        IHttpClientFactory httpClientFactory,
        ILogger<GoogleFreeLocalizationTranslationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<Dictionary<string, LocalizedText>> TranslateFromVietnameseAsync(
        string vietnameseName,
        string vietnameseDescription,
        IReadOnlyCollection<string> targetLanguageCodes,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, LocalizedText>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(vietnameseName) && string.IsNullOrWhiteSpace(vietnameseDescription))
        {
            return result;
        }

        using var client = _httpClientFactory.CreateClient("GoogleFreeTranslation");

        foreach (var targetLang in targetLanguageCodes)
        {
            try
            {
                var langCode = MapToGoogleLangCode(targetLang);
                
                string translatedName = vietnameseName;
                if (!string.IsNullOrWhiteSpace(vietnameseName))
                {
                    translatedName = await TranslateTextAsync(client, vietnameseName, langCode, cancellationToken);
                }

                string translatedDescription = vietnameseDescription;
                if (!string.IsNullOrWhiteSpace(vietnameseDescription))
                {
                    translatedDescription = await TranslateTextAsync(client, vietnameseDescription, langCode, cancellationToken);
                }

                result[targetLang] = new LocalizedText
                {
                    LocalizedName = translatedName,
                    LocalizedDescription = translatedDescription
                };
                
                // Be gentle with the free API
                await Task.Delay(500, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Free translation failed for {LangCode}", targetLang);
                throw new InvalidOperationException($"Lỗi gọi Google Translate miễn phí: {ex.Message}");
            }
        }

        return result;
    }

    private async Task<string> TranslateTextAsync(HttpClient client, string text, string targetLang, CancellationToken cancellationToken)
    {
        var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=vi&tl={targetLang}&dt=t&q={Uri.EscapeDataString(text)}";
        
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        
        // Google Translate returns a messy array structure: [[["Translated","Original",null,null,1]],null,"vi",...]
        using var doc = JsonDocument.Parse(json);
        
        if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
        {
            var firstArray = doc.RootElement[0];
            if (firstArray.ValueKind == JsonValueKind.Array)
            {
                var translatedText = string.Empty;
                // It splits long text into multiple segments inside the array
                foreach (var segment in firstArray.EnumerateArray())
                {
                    if (segment.ValueKind == JsonValueKind.Array && segment.GetArrayLength() > 0)
                    {
                        var chunk = segment[0];
                        if (chunk.ValueKind == JsonValueKind.String)
                        {
                            translatedText += chunk.GetString();
                        }
                    }
                }
                return translatedText.Trim();
            }
        }

        return text;
    }

    private string MapToGoogleLangCode(string standardCode)
    {
        return standardCode switch
        {
            "en-US" => "en",
            "zh-CN" => "zh-CN",
            "ja-JP" => "ja",
            "ko-KR" => "ko",
            _ => standardCode.Split('-')[0]
        };
    }
}
