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
/// Auto-translation using a local/self-hosted Ollama server.
/// Translates from Vietnamese (vi-VN) into multiple target languages.
/// </summary>
public sealed class OllamaLocalizationTranslationService : ILocalizationTranslationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OllamaLocalizationTranslationService> _logger;

    public OllamaLocalizationTranslationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OllamaLocalizationTranslationService> logger)
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

        var baseUrl = (_configuration["Ollama:BaseUrl"] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new LocalizationTranslationNotConfiguredException(
                "Chưa cấu hình dịch tự động. Hãy cài Ollama và cấu hình Ollama:BaseUrl (mặc định: http://localhost:11434)." );
        }

        var model = (_configuration["Ollama:Model"] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new LocalizationTranslationNotConfiguredException(
                "Chưa cấu hình model dịch tự động. Hãy đặt Ollama:Model (ví dụ: qwen2.5:3b) và đảm bảo model đã được pull (ollama pull qwen2.5:3b)." );
        }

        // Quality strategy: translate one language per request.
        // This improves faithfulness and reduces JSON formatting errors.
        using var client = _httpClientFactory.CreateClient("OllamaTranslation");
        var endpoint = new Uri(new Uri(baseUrl.TrimEnd('/') + "/", UriKind.Absolute), "api/chat");

        var output = new Dictionary<string, LocalizedText>(StringComparer.OrdinalIgnoreCase);

        foreach (var lang in distinctTargets)
        {
            output[lang] = await TranslateSingleLanguageWithRetryAsync(
                client,
                endpoint,
                model,
                vietnameseName,
                vietnameseDescription,
                lang,
                cancellationToken);
        }

        return output;
    }

    private async Task<LocalizedText> TranslateSingleLanguageWithRetryAsync(
        HttpClient client,
        Uri endpoint,
        string model,
        string vietnameseName,
        string vietnameseDescription,
        string languageCode,
        CancellationToken cancellationToken)
    {
        try
        {
            return await TranslateSingleLanguageOnceAsync(
                client,
                endpoint,
                model,
                vietnameseName,
                vietnameseDescription,
                languageCode,
                isRetry: false,
                cancellationToken);
        }
        catch (InvalidOperationException ex) when (IsLikelyFormatOrValidationIssue(ex.Message))
        {
            _logger.LogInformation(
                "Retrying Ollama translation for {LanguageCode} due to format/validation issue.",
                languageCode);

            return await TranslateSingleLanguageOnceAsync(
                client,
                endpoint,
                model,
                vietnameseName,
                vietnameseDescription,
                languageCode,
                isRetry: true,
                cancellationToken);
        }
    }

    private async Task<LocalizedText> TranslateSingleLanguageOnceAsync(
        HttpClient client,
        Uri endpoint,
        string model,
        string vietnameseName,
        string vietnameseDescription,
        string languageCode,
        bool isRetry,
        CancellationToken cancellationToken)
    {
        var systemPrompt = BuildSystemPrompt(languageCode);
        var userPrompt = BuildUserPromptSingle(vietnameseName, vietnameseDescription, languageCode, isRetry);

        var requestBody = new
        {
            model,
            stream = false,
            // Ask Ollama to return strict JSON if supported. Older versions ignore unknown fields.
            format = "json",
            options = new
            {
                // Lower temperature for faithful translation.
                temperature = isRetry ? 0.0 : 0.1,
                top_p = 0.9,
                // Keep context smaller to reduce RAM usage (useful on low-memory machines).
                num_ctx = 2048
            },
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
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
            _logger.LogWarning(ex, "Ollama translation request failed.");
            throw new InvalidOperationException(
                "Không gọi được Ollama để dịch tự động. Hãy đảm bảo Ollama đang chạy (mặc định http://localhost:11434) và thử lại.");
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Ollama translation failed. Status={Status}. Body={Body}",
                (int)response.StatusCode,
                body);

            throw new InvalidOperationException(BuildUserFacingOllamaErrorMessage((int)response.StatusCode, body, model));
        }

        var assistantText = ExtractOllamaAssistantText(body);
        if (string.IsNullOrWhiteSpace(assistantText))
        {
            throw new InvalidOperationException("Ollama translation response was empty.");
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
            _logger.LogWarning(ex, "Failed to parse Ollama JSON. RawText={RawText}", assistantText);
            throw new InvalidOperationException("Ollama trả về JSON không hợp lệ.");
        }

        if (localized is null)
        {
            throw new InvalidOperationException("Ollama translation returned null JSON.");
        }

        localized.LocalizedName = (localized.LocalizedName ?? string.Empty).Trim();
        localized.LocalizedDescription = (localized.LocalizedDescription ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(localized.LocalizedName)
            || string.IsNullOrWhiteSpace(localized.LocalizedDescription))
        {
            throw new InvalidOperationException("Ollama response missing localizedName/localizedDescription.");
        }

        return localized;
    }

    private static string BuildSystemPrompt(string languageCode)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Bạn là dịch giả chuyên nghiệp cho nội dung thuyết minh du lịch Việt Nam.");
        sb.AppendLine("Ưu tiên dịch SÁT NGHĨA, trung thành với tiếng Việt: không rút gọn, không phóng đại, không thêm thông tin mới.");
        sb.AppendLine("Giữ sắc thái/ngữ nghĩa gốc, càng gần ý tiếng Việt càng tốt.");
        sb.AppendLine("Giữ nguyên mọi con số/đơn vị/ký hiệu (ví dụ: 10m, 2km, 50.000đ).");
        sb.AppendLine("Giữ tên riêng/địa danh/thương hiệu/món ăn theo tiếng Việt nếu phù hợp; nếu có bản dịch phổ biến (ví dụ Ho Chi Minh City) thì có thể dùng.");
        sb.AppendLine("Ưu tiên từ vựng CHUẨN MỰC, giàu sắc thái (hơi trang trọng) thay vì từ đơn giản; tránh tiếng lóng/văn nói.");
        sb.AppendLine("Văn phong tự nhiên, lịch sự, dễ nghe khi đọc TTS.");
        sb.AppendLine("Trước khi trả kết quả: tự kiểm tra nhanh bằng cách dịch ngược về tiếng Việt trong đầu để chắc chắn không lệch nghĩa.");
        sb.AppendLine("Luôn trả về DUY NHẤT JSON hợp lệ (không markdown, không giải thích, không ký tự thừa).");
        sb.AppendLine($"Ngôn ngữ đầu ra: {DescribeTargetLanguage(languageCode)}.");
        return sb.ToString();
    }

    private static string BuildUserPromptSingle(
        string vietnameseName,
        string vietnameseDescription,
        string targetLanguageCode,
        bool isRetry)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Nhiệm vụ: Dịch chính xác nội dung tiếng Việt sang {DescribeTargetLanguage(targetLanguageCode)}.");
        sb.AppendLine("Ràng buộc bắt buộc:");
        sb.AppendLine("- TRẢ VỀ DUY NHẤT JSON (không markdown, không giải thích, không dấu ```).");
        sb.AppendLine("- JSON schema đúng y hệt: {\"localizedName\":\"...\",\"localizedDescription\":\"...\"}.");
        sb.AppendLine("- localizedName/localizedDescription KHÔNG được rỗng.");
        sb.AppendLine("- Không thêm thông tin mới, không bịa địa chỉ/giá/khuyến mãi.");
        sb.AppendLine("- Không đổi nghĩa, không tóm tắt.");
        sb.AppendLine("- Dùng từ vựng phong phú/chính xác hơn (có thể hơi trang trọng) nhưng KHÔNG được thêm ý.");
        sb.AppendLine("- Nếu không chắc cách dịch tên riêng/món ăn, giữ nguyên tiếng Việt để tránh dịch sai.");

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
            || message.Contains("empty", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractOllamaAssistantText(string responseBodyJson)
    {
        using var doc = JsonDocument.Parse(responseBodyJson);

        // /api/chat
        if (doc.RootElement.TryGetProperty("message", out var message)
            && message.ValueKind == JsonValueKind.Object
            && message.TryGetProperty("content", out var contentEl)
            && contentEl.ValueKind == JsonValueKind.String)
        {
            return contentEl.GetString() ?? string.Empty;
        }

        // /api/generate (fallback)
        if (doc.RootElement.TryGetProperty("response", out var responseEl)
            && responseEl.ValueKind == JsonValueKind.String)
        {
            return responseEl.GetString() ?? string.Empty;
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

    private static string BuildUserFacingOllamaErrorMessage(int httpStatusCode, string responseBody, string model)
    {
        var error = TryParseOllamaError(responseBody);
        error = (error ?? string.Empty).Trim();

        if (!string.IsNullOrWhiteSpace(error)
            && error.Contains("model", StringComparison.OrdinalIgnoreCase)
            && error.Contains("not", StringComparison.OrdinalIgnoreCase)
            && error.Contains("found", StringComparison.OrdinalIgnoreCase))
        {
            return $"Ollama chưa có model '{model}'. Hãy chạy: ollama pull {model}";
        }

        if (httpStatusCode == 404)
        {
            return "Không tìm thấy endpoint của Ollama (404). Hãy kiểm tra Ollama:BaseUrl (mặc định http://localhost:11434).";
        }

        if (httpStatusCode == 401 || httpStatusCode == 403)
        {
            return "Ollama bị từ chối truy cập (401/403). Hãy kiểm tra cấu hình reverse proxy hoặc quyền truy cập tới Ollama.";
        }

        if (httpStatusCode == 429)
        {
            return "Ollama đang bận (429). Hãy thử lại sau.";
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            return $"Gọi Ollama thất bại ({httpStatusCode}). {error}";
        }

        return $"Gọi Ollama thất bại ({httpStatusCode}). Hãy kiểm tra Ollama đang chạy và model '{model}' có sẵn.";
    }

    private static string? TryParseOllamaError(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("error", out var errorEl)
                && errorEl.ValueKind == JsonValueKind.String)
            {
                return errorEl.GetString();
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }
}
