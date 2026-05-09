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

    public static string GetApiBaseUrl()
        => Preferences.Default.Get(ApiBaseUrlKey, string.Empty).Trim();

    public static bool TryGetApiBaseUrl(out Uri? apiBaseUrl)
    {
        apiBaseUrl = null;
        var raw = GetApiBaseUrl();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (!IsHttpOrHttps(parsed))
        {
            return false;
        }

        apiBaseUrl = EnsureTrailingSlash(parsed);
        return true;
    }

    public static bool SetApiBaseUrl(string? apiBaseUrl)
    {
        var normalized = NormalizeApiBaseUrl(apiBaseUrl);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            Preferences.Default.Remove(ApiBaseUrlKey);
            return true;
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var parsed) || !IsHttpOrHttps(parsed))
        {
            return false;
        }

        Preferences.Default.Set(ApiBaseUrlKey, EnsureTrailingSlash(parsed).AbsoluteUri);
        return true;
    }

    public static void ClearApiBaseUrl()
        => Preferences.Default.Remove(ApiBaseUrlKey);

    private static bool IsHttpOrHttps(Uri uri)
        => uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeApiBaseUrl(string? apiBaseUrl)
    {
        var normalized = (apiBaseUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return normalized;
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        var absoluteUri = uri.AbsoluteUri;
        return absoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri(absoluteUri + "/");
    }
}
