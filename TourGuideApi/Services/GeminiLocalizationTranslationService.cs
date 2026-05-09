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
/// - Gemini:ApiKey
/// - Gemini:Model (e.g., gemini-1.5-flash)
/// - Gemini:BaseUrl (optional, default https://generativelanguage.googleapis.com)
/// </summary>
public sealed class GeminiLocalizationTranslationService : ILocalizationTranslationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiLocalizationTranslationService> _logger;

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
            throw new LocalizationTranslationNotConfiguredException(
                "Chưa cấu hình Gemini API key. Hãy đặt Gemini:ApiKey (khuyến nghị trong TourGuideApi/appsettings.json hoặc env var Gemini__ApiKey) để bật auto-translate.");
        }

        var baseUrl = (_configuration["Gemini:BaseUrl"] ?? "https://generativelanguage.googleapis.com").Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "https://generativelanguage.googleapis.com";
        }

        var model = (_configuration["Gemini:Model"] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(model))
        {
            // Keep a sane default; users can override in appsettings.json or environment variables.
            model = "gemini-1.5-flash";
        }

        var protectedTerms = _configuration
            .GetSection("Gemini:ProtectedTerms")
            .Get<string[]>()
            ?.Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? Array.Empty<string>();

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
                protectedTerms,
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
        IReadOnlyCollection<string> protectedTerms,
        CancellationToken cancellationToken)
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
                protectedTerms,
                isRetry: false,
                cancellationToken);
        }
        catch (InvalidOperationException ex) when (IsLikelyFormatOrValidationIssue(ex.Message))
        {
            _logger.LogInformation(
                "Retrying Gemini translation for {LanguageCode} due to format/validation issue.",
                languageCode);

            return await TranslateSingleLanguageOnceAsync(
                client,
                baseUrl,
                model,
                apiKey,
                vietnameseName,
                vietnameseDescription,
                languageCode,
                protectedTerms,
                isRetry: true,
                cancellationToken);
        }
    }

    private async Task<LocalizedText> TranslateSingleLanguageOnceAsync(
        HttpClient client,
        string baseUrl,
        string model,
        string apiKey,
        string vietnameseName,
        string vietnameseDescription,
        string languageCode,
        IReadOnlyCollection<string> protectedTerms,
        bool isRetry,
        CancellationToken cancellationToken)
    {
        // API: POST https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}
        // We keep this configurable via Gemini:BaseUrl + Gemini:Model.
        var endpoint = BuildEndpoint(baseUrl, model, apiKey);

        var systemPrompt = BuildSystemPrompt(languageCode, protectedTerms);
        var userPrompt = BuildUserPromptSingle(vietnameseName, vietnameseDescription, languageCode, protectedTerms, isRetry);

        // Avoid relying on newer optional fields; provide one combined prompt.
        // Gemini can still follow JSON-only instructions, but we keep strict parsing + retry.
        var requestBody = new
        {
            contents = new object[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new
                        {
                            text = systemPrompt + "\n\n" + userPrompt
                        }
                    }
                }
            },
            generationConfig = new
            {
                temperature = isRetry ? 0.0 : 0.1,
                topP = 0.9,
                maxOutputTokens = 1024
            }
        };

        HttpResponseMessage response;
        string body;
        try
        {
            response = await client.PostAsJsonAsync(endpoint, requestBody, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini translation request failed.");
            throw new InvalidOperationException(
                "Không gọi được Gemini để dịch tự động. Hãy kiểm tra kết nối Internet và cấu hình Gemini:ApiKey/Gemini:Model.");
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Gemini translation failed. Status={Status}. Body={Body}",
                (int)response.StatusCode,
                body);

            throw new InvalidOperationException(BuildUserFacingGeminiErrorMessage((int)response.StatusCode, body));
        }

        var assistantText = ExtractGeminiText(body);
        if (string.IsNullOrWhiteSpace(assistantText))
        {
            throw new InvalidOperationException("Gemini translation response was empty.");
        }

        var json = TryExtractJsonObject(assistantText);

        LocalizedText? localized;
        try
        {
            localized = JsonSerializer.Deserialize<LocalizedText>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Gemini JSON. RawText={RawText}", assistantText);
            throw new InvalidOperationException("Gemini trả về JSON không hợp lệ.");
        }

        if (localized is null)
        {
            throw new InvalidOperationException("Gemini translation returned null JSON.");
        }

        localized.LocalizedName = (localized.LocalizedName ?? string.Empty).Trim();
        localized.LocalizedDescription = (localized.LocalizedDescription ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(localized.LocalizedName)
            || string.IsNullOrWhiteSpace(localized.LocalizedDescription))
        {
            throw new InvalidOperationException("Gemini response missing localizedName/localizedDescription.");
        }

        return localized;
    }

    private static Uri BuildEndpoint(string baseUrl, string model, string apiKey)
    {
        var trimmedBaseUrl = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
        var trimmedModel = (model ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(trimmedBaseUrl))
        {
            trimmedBaseUrl = "https://generativelanguage.googleapis.com";
        }

        if (string.IsNullOrWhiteSpace(trimmedModel))
        {
            trimmedModel = "gemini-1.5-flash";
        }

        var builder = new UriBuilder(trimmedBaseUrl)
        {
            Path = $"/v1beta/models/{trimmedModel}:generateContent",
            Query = $"key={Uri.EscapeDataString(apiKey ?? string.Empty)}"
        };

        return builder.Uri;
    }

    private static string BuildSystemPrompt(string languageCode, IReadOnlyCollection<string> protectedTerms)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Bạn là dịch giả chuyên nghiệp cho nội dung thuyết minh du lịch Việt Nam.");
        sb.AppendLine("Mục tiêu: dịch sát nghĩa, đúng ngữ cảnh, tự nhiên khi đọc TTS, tuyệt đối không dịch tầm bậy.");
        sb.AppendLine("Không thêm thông tin mới, không rút gọn, không phóng đại, không đổi giọng văn.");
        sb.AppendLine("Giữ nguyên mọi con số/đơn vị/ký hiệu (ví dụ: 10m, 2km, 50.000đ).");
        sb.AppendLine("Không dịch tên quán/nhãn hiệu/tên riêng/tên người/tên món ăn/tên địa danh nếu đó là tên gọi thực tế hoặc viết hoa như một danh xưng riêng.");
        sb.AppendLine("Nếu một địa danh có bản dịch phổ biến và chuẩn mực (ví dụ Ho Chi Minh City) thì chỉ dùng khi thực sự cần, còn lại giữ nguyên.");
        sb.AppendLine("Ưu tiên dịch sát nghĩa, giữ cấu trúc câu gốc khi có thể, tránh diễn giải quá đà.");
        sb.AppendLine("Văn phong tự nhiên, lịch sự, rõ ràng, dễ nghe khi đọc TTS.");
        if (protectedTerms.Count > 0)
        {
            sb.AppendLine("Các cụm từ khóa bắt buộc GIỮ NGUYÊN (không dịch):");
            foreach (var term in protectedTerms)
            {
                sb.AppendLine($"- {term}");
            }
        }
        sb.AppendLine("Luôn trả về DUY NHẤT JSON hợp lệ (không markdown, không giải thích, không ký tự thừa).");
        sb.AppendLine($"Ngôn ngữ đầu ra: {DescribeTargetLanguage(languageCode)}.");
        return sb.ToString();
    }

    private static string BuildUserPromptSingle(
        string vietnameseName,
        string vietnameseDescription,
        string targetLanguageCode,
        IReadOnlyCollection<string> protectedTerms,
        bool isRetry)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Nhiệm vụ: Dịch chính xác nội dung tiếng Việt sang {DescribeTargetLanguage(targetLanguageCode)}.");
        sb.AppendLine("Ràng buộc bắt buộc:");
        sb.AppendLine("- TRẢ VỀ DUY NHẤT JSON (không markdown, không giải thích, không dấu ```).");
        sb.AppendLine("- JSON schema đúng y hệt: {\"localizedName\":\"...\",\"localizedDescription\":\"...\"}.");
        sb.AppendLine("- localizedName/localizedDescription KHÔNG được rỗng.");
        sb.AppendLine("- Dịch sát nghĩa từng ý, không sáng tác nội dung mới.");
        sb.AppendLine("- Không thêm địa chỉ/giá/khuyến mãi nếu không có trong tiếng Việt gốc.");
        sb.AppendLine("- Giữ nguyên tên quán, tên người, tên món ăn, tên địa danh nếu đó là tên riêng hoặc viết hoa như danh xưng riêng.");
        sb.AppendLine("- Nếu một từ/cụm từ là tên riêng mà có nguy cơ dịch sai, hãy giữ nguyên tiếng Việt.");
        if (protectedTerms.Count > 0)
        {
            sb.AppendLine("- Bắt buộc giữ nguyên các từ khóa sau (không dịch):");
            foreach (var term in protectedTerms)
            {
                sb.AppendLine($"  - {term}");
            }
        }

        if (isRetry)
        {
            sb.AppendLine();
            sb.AppendLine("LƯU Ý: Lần trước định dạng sai. Lần này chỉ được trả về JSON đúng schema, không thêm bất kỳ ký tự nào khác.");
        }

        sb.AppendLine();
        sb.AppendLine("Dữ liệu tiếng Việt:");
        sb.AppendLine($"- vietnameseName: {vietnameseName}");
        sb.AppendLine($"- vietnameseDescription: {vietnameseDescription}");
        return sb.ToString();
    }

    private static string DescribeTargetLanguage(string languageCode)
    {
        return (languageCode ?? string.Empty).Trim() switch
        {
            "en-US" => "English (en-US)",
            "zh-CN" => "Simplified Chinese (zh-CN, 中文简体)",
            "ja-JP" => "Japanese (ja-JP, 日本語)",
            "ko-KR" => "Korean (ko-KR, 한국어)",
            _ => $"{languageCode}"
        };
    }

    private static bool IsLikelyFormatOrValidationIssue(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("json", StringComparison.OrdinalIgnoreCase)
            || message.Contains("missing", StringComparison.OrdinalIgnoreCase)
            || message.Contains("schema", StringComparison.OrdinalIgnoreCase)
            || message.Contains("empty", StringComparison.OrdinalIgnoreCase)
            || message.Contains("invalid", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractGeminiText(string responseBodyJson)
    {
        using var doc = JsonDocument.Parse(responseBodyJson);

        if (doc.RootElement.TryGetProperty("candidates", out var candidates)
            && candidates.ValueKind == JsonValueKind.Array
            && candidates.GetArrayLength() > 0)
        {
            var first = candidates[0];
            
            if (first.TryGetProperty("finishReason", out var finishReason) && finishReason.ValueKind == JsonValueKind.String)
            {
                var reason = finishReason.GetString();
                if (reason == "SAFETY" || reason == "RECITATION" || reason == "OTHER")
                {
                    throw new InvalidOperationException($"Bị Gemini từ chối với lý do: {reason}. Có thể do nội dung vi phạm chính sách hoặc từ khóa nhạy cảm.");
                }
            }

            if (first.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.Object
                && content.TryGetProperty("parts", out var parts)
                && parts.ValueKind == JsonValueKind.Array
                && parts.GetArrayLength() > 0)
            {
                var part = parts[0];
                if (part.ValueKind == JsonValueKind.Object
                    && part.TryGetProperty("text", out var textEl)
                    && textEl.ValueKind == JsonValueKind.String)
                {
                    return textEl.GetString() ?? string.Empty;
                }
            }
        }

        // Check for top-level promptFeedback block
        if (doc.RootElement.TryGetProperty("promptFeedback", out var promptFeedback)
            && promptFeedback.ValueKind == JsonValueKind.Object
            && promptFeedback.TryGetProperty("blockReason", out var blockReason))
        {
            throw new InvalidOperationException($"Bị Gemini từ chối ở cấp độ Prompt. Lý do: {blockReason.GetString()}");
        }

        return string.Empty;
    }

    private static string TryExtractJsonObject(string text)
    {
        var trimmed = (text ?? string.Empty).Trim();

        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = trimmed.IndexOf('\n');
            if (firstNewLine >= 0)
            {
                trimmed = trimmed[(firstNewLine + 1)..];
            }

            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence >= 0)
            {
                trimmed = trimmed[..lastFence];
            }

            trimmed = trimmed.Trim();
        }

        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');

        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
        }

        return trimmed;
    }

    private static string BuildUserFacingGeminiErrorMessage(int httpStatusCode, string responseBody)
    {
        var error = TryParseGeminiError(responseBody);
        error = (error ?? string.Empty).Trim();

        if (httpStatusCode == 400)
        {
            return !string.IsNullOrWhiteSpace(error)
                ? $"Gemini trả về lỗi (400). {error}"
                : "Gemini trả về lỗi (400). Hãy kiểm tra Gemini:Model và nội dung yêu cầu.";
        }

        if (httpStatusCode == 401 || httpStatusCode == 403)
        {
            return "Gemini bị từ chối truy cập (401/403). Hãy kiểm tra Gemini:ApiKey (và quota/billing nếu có).";
        }

        if (httpStatusCode == 429)
        {
            return "Gemini đang bị giới hạn (429). Hãy thử lại sau hoặc nâng quota.";
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            return $"Gọi Gemini thất bại ({httpStatusCode}). {error}";
        }

        return $"Gọi Gemini thất bại ({httpStatusCode}).";
    }

    private static string? TryParseGeminiError(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("error", out var errorEl)
                && errorEl.ValueKind == JsonValueKind.Object)
            {
                if (errorEl.TryGetProperty("message", out var messageEl)
                    && messageEl.ValueKind == JsonValueKind.String)
                {
                    return messageEl.GetString();
                }

                if (errorEl.TryGetProperty("status", out var statusEl)
                    && statusEl.ValueKind == JsonValueKind.String)
                {
                    return statusEl.GetString();
                }
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }
}
