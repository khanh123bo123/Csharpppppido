using System.Collections.Generic;

namespace TouristGuideApp.Services;

public static class SupportedLanguages
{
    public const string Vietnamese = "vi-VN";
    public const string English = "en-US";
    public const string Chinese = "zh-CN";
    public const string Japanese = "ja-JP";
    public const string Korean = "ko-KR";

    public static IReadOnlyList<string> AllLanguages { get; } = new[]
    {
        Vietnamese,
        English,
        Chinese,
        Japanese,
        Korean
    };

    public static IReadOnlyDictionary<string, string> LanguageNames { get; } =
        new Dictionary<string, string>
        {
            [Vietnamese] = "Tiếng Việt",
            [English] = "English",
            [Chinese] = "中文",
            [Japanese] = "日本語",
            [Korean] = "한국어"
        };
}
