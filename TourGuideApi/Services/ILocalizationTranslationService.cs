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
