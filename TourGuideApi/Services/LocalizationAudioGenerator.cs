using Microsoft.EntityFrameworkCore;
using TourGuideApi.Data;

namespace TourGuideApi.Services;

public sealed class LocalizationAudioGenerator
{
    private readonly AppDbContext _context;
    private readonly ITextToSpeechService _tts;
    private readonly ILogger<LocalizationAudioGenerator> _logger;

    public LocalizationAudioGenerator(AppDbContext context, ITextToSpeechService tts, ILogger<LocalizationAudioGenerator> logger)
    {
        _context = context;
        _tts = tts;
        _logger = logger;
    }

    public async Task GenerateAsync(int localizationId, CancellationToken cancellationToken)
    {
        var localization = await _context.Localizations
            .FirstOrDefaultAsync(l => l.Id == localizationId, cancellationToken);

        if (localization is null)
        {
            return;
        }

        try
        {
            var voiceCode = localization.TtsVoiceCode ?? GetDefaultVoice(localization.LanguageCode);
            var audioBase64 = await _tts.GenerateSpeechAsync(
                localization.LocalizedDescription,
                localization.LanguageCode,
                voiceCode);

            // Some providers may return a JSON instruction instead of actual audio.
            // For the web/admin Tier-2 use-case, treat this as a failure.
            if (string.IsNullOrWhiteSpace(audioBase64)
                || audioBase64.Contains("EDGE_TTS", StringComparison.OrdinalIgnoreCase))
            {
                localization.AudioGenerationStatus = "failed";
                localization.UpdatedAt = DateTime.UtcNow;
                _context.Localizations.Update(localization);
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            localization.CachedAudioBase64 = audioBase64;
            localization.AudioGenerationStatus = "generated";
            localization.CachedAudioUrl = $"/api/localizations/{localization.Id}/audio";
            localization.UpdatedAt = DateTime.UtcNow;

            _context.Localizations.Update(localization);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Audio generated for localization {LocalizationId}", localization.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate audio for localization {LocalizationId}", localization.Id);
            try
            {
                localization.AudioGenerationStatus = "failed";
                localization.UpdatedAt = DateTime.UtcNow;
                _context.Localizations.Update(localization);
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                // ignore
            }
        }
    }

    private static string GetDefaultVoice(string languageCode)
    {
        return languageCode switch
        {
            "vi-VN" => "vi-VN-HoaiMyNeural",
            "en-US" => "en-US-AriaNeural",
            "zh-CN" => "zh-CN-XiaoxiaoNeural",
            "ja-JP" => "ja-JP-NanamiNeural",
            "ko-KR" => "ko-KR-SunHiNeural",
            _ => "en-US-AriaNeural"
        };
    }
}
