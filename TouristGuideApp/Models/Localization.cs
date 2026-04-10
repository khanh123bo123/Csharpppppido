namespace TouristGuideApp.Models;

/// <summary>
/// Localization model for multi-language POI support
/// Maps to backend Localization entity
/// </summary>
public class LocalizationModel
{
    public int Id { get; set; }
    public int LocationId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string LocalizedName { get; set; } = string.Empty;
    public string LocalizedDescription { get; set; } = string.Empty;
    public string? CachedAudioUrl { get; set; }
    public string? TextToSpeechEndpoint { get; set; }
    public string AudioGenerationStatus { get; set; } = string.Empty;
    public string? TtsVoiceCode { get; set; }
    public bool IsWarmupProcessed { get; set; }
}

/// <summary>
/// Supported languages in the system
/// </summary>
public static class SupportedLanguages
{
    public const string Vietnamese = "vi-VN";
    public const string English = "en-US";
    public const string ChineseSimplified = "zh-CN";
    public const string Japanese = "ja-JP";
    public const string Korean = "ko-KR";

    public static readonly string[] AllLanguages = { Vietnamese, English, ChineseSimplified, Japanese, Korean };
    
    public static readonly Dictionary<string, string> LanguageNames = new()
    {
        { Vietnamese, "Tiếng Việt" },
        { English, "English" },
        { ChineseSimplified, "简体中文" },
        { Japanese, "日本語" },
        { Korean, "한국어" }
    };
}
