using Microsoft.Maui.Storage;
using TouristGuideApp.Models;

namespace TouristGuideApp.Services;

public static class AppPreferences
{
    public const string NarrationLanguageCodeKey = "NarrationLanguageCode";
    public const string NarrationSpeechRateKey = "NarrationSpeechRate";
    public const string ApiBaseUrlKey = "ApiBaseUrl";

    public static bool HasNarrationLanguageCode()
        => Preferences.Default.ContainsKey(NarrationLanguageCodeKey);

    public static string GetNarrationLanguageCode()
        => Preferences.Default.Get(NarrationLanguageCodeKey, SupportedLanguages.Vietnamese);

    public static void SetNarrationLanguageCode(string? languageCode)
    {
        var code = string.IsNullOrWhiteSpace(languageCode)
            ? SupportedLanguages.Vietnamese
            : languageCode.Trim();

        Preferences.Default.Set(NarrationLanguageCodeKey, code);
    }

    public static double GetNarrationSpeechRate()
        => Preferences.Default.Get(NarrationSpeechRateKey, 0.25);

    public static void SetNarrationSpeechRate(double speechRate)
    {
        if (double.IsNaN(speechRate) || double.IsInfinity(speechRate))
        {
            return;
        }

        // Keep within a sane range; 0.25 is supported but very slow.
        speechRate = Math.Clamp(speechRate, 0.1, 4.0);
        Preferences.Default.Set(NarrationSpeechRateKey, speechRate);
    }

    public static string? GetApiBaseUrl()
    {
        var value = Preferences.Default.Get<string?>(ApiBaseUrlKey, null);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static void SetApiBaseUrl(string? apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            Preferences.Default.Remove(ApiBaseUrlKey);
            return;
        }

        Preferences.Default.Set(ApiBaseUrlKey, apiBaseUrl.Trim());
    }
}
