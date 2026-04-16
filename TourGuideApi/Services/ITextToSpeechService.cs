namespace TourGuideApi.Services;

/// <summary>
/// Hybrid Audio System:
/// TIER 1: Local cache (SQLite blob or file system)
/// TIER 2: Edge-TTS CLI (no API key; requires internet)
/// TIER 3: Browser/Client TTS (fallback)
/// </summary>
public interface ITextToSpeechService
{
    /// <summary>
    /// Generate audio using 4-tier system with automatic fallback.
    /// Returns audio file as base64 string.
    /// </summary>
    Task<string> GenerateSpeechAsync(string text, string languageCode, string voiceCode);

    /// <summary>
    /// Preprocess (warmup) all localizations for a location on creation.
    /// Called when admin adds a new POI to pre-generate all language variants.
    /// </summary>
    Task WarmupLocalizationsAsync(int locationId);

    /// <summary>
    /// Get available voices for a given language
    /// </summary>
    Task<string[]> GetAvailableVoicesAsync(string languageCode);

    /// <summary>
    /// Legacy method: Synthesize with file name
    /// </summary>
    Task<string> SynthesizeAsync(string text, string fileName);
}


