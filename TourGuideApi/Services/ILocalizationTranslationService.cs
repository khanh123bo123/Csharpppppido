using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TourGuideApi.Services;

public interface ILocalizationTranslationService
{
    Task<Dictionary<string, LocalizedText>> TranslateFromVietnameseAsync(
        string vietnameseName,
        string vietnameseDescription,
        IReadOnlyCollection<string> targetLanguageCodes,
        CancellationToken cancellationToken = default);
}

public sealed class LocalizationTranslationNotConfiguredException : InvalidOperationException
{
    public LocalizationTranslationNotConfiguredException(string message) : base(message)
    {
    }
}

public sealed class LocalizedText
{
    public string LocalizedName { get; set; } = string.Empty;
    public string LocalizedDescription { get; set; } = string.Empty;
}

public sealed class DisabledLocalizationTranslationService : ILocalizationTranslationService
{
    public Task<Dictionary<string, LocalizedText>> TranslateFromVietnameseAsync(
        string vietnameseName,
        string vietnameseDescription,
        IReadOnlyCollection<string> targetLanguageCodes,
        CancellationToken cancellationToken = default)
    {
        throw new LocalizationTranslationNotConfiguredException(
            "Chưa cấu hình dịch tự động. Hãy cấu hình Translation:Provider và một trong các lựa chọn sau: " +
            "(1) Gemini:ApiKey (+ Gemini:Model) để dịch online, hoặc " +
            "(2) Ollama:BaseUrl + Ollama:Model để dịch offline. " +
            "Khuyến nghị đặt trong TourGuideApi/appsettings.Local.json hoặc environment variables.");
    }
}
