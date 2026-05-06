using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using TourGuideApi.Data;
using TourGuideApi.Models;

namespace TourGuideApi.Services;

public class LocalizationPackGenerator
{
    private readonly AppDbContext _context;
    private readonly ILocalizationTranslationService _translationService;
    private readonly IAudioGenerationQueue _audioQueue;
    private readonly ILogger<LocalizationPackGenerator> _logger;

    private static readonly string[] SupportedLanguages = { "vi-VN", "en-US", "zh-CN", "ja-JP", "ko-KR" };

    public LocalizationPackGenerator(
        AppDbContext context,
        ILocalizationTranslationService translationService,
        IAudioGenerationQueue audioQueue,
        ILogger<LocalizationPackGenerator> logger)
    {
        _context = context;
        _translationService = translationService;
        _audioQueue = audioQueue;
        _logger = logger;
    }

    public async Task GeneratePackAsync(int locationId, string vietnameseName, string vietnameseDescription, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting background generation for Location {LocationId}", locationId);
        
        var location = await _context.Locations.FindAsync(new object[] { locationId }, cancellationToken);
        if (location == null)
        {
            _logger.LogWarning("Location {LocationId} not found, aborting pack generation.", locationId);
            return;
        }

        // 1. NGAY LẬP TỨC TẠO DỮ LIỆU "CHỜ" VÀO DB ĐỂ UI HIỂN THỊ TRƯỚC
        var now = DateTime.UtcNow;
        var touched = new List<Localization>();

        foreach (var languageCode in SupportedLanguages)
        {
            var existing = await _context.Localizations.FirstOrDefaultAsync(
                l => l.LocationId == locationId && l.LanguageCode == languageCode, cancellationToken);

            if (existing == null)
            {
                var localization = new Localization
                {
                    LocationId = locationId,
                    LanguageCode = languageCode,
                    LocalizedName = languageCode == "vi-VN" ? vietnameseName : "(Translating...)",
                    LocalizedDescription = languageCode == "vi-VN" ? vietnameseDescription : "(Translating...)",
                    TtsVoiceCode = GetDefaultVoice(languageCode),
                    AudioGenerationStatus = languageCode == "vi-VN" ? "pending" : "translating",
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _context.Localizations.Add(localization);
                touched.Add(localization);
                
                if (languageCode == "vi-VN") 
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    _audioQueue.TryEnqueue(localization.Id);
                }
            }
            else
            {
                existing.AudioGenerationStatus = languageCode == "vi-VN" ? "pending" : "translating";
                existing.UpdatedAt = now;
                _context.Localizations.Update(existing);
                touched.Add(existing);
                
                if (languageCode == "vi-VN") 
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    _audioQueue.TryEnqueue(existing.Id);
                }
            }
        }
        await _context.SaveChangesAsync(cancellationToken);

        // 2. BẮT ĐẦU DỊCH TỰ ĐỘNG (quá trình này mất vài phút)
        var targetLanguages = SupportedLanguages
            .Where(l => !string.Equals(l, "vi-VN", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Dictionary<string, LocalizedText> translated;
        try
        {
            _logger.LogInformation("Requesting external translation for {Count} languages for Location {LocationId}", targetLanguages.Length, locationId);
            translated = await _translationService.TranslateFromVietnameseAsync(
                vietnameseName,
                vietnameseDescription,
                targetLanguages,
                cancellationToken);
            _logger.LogInformation("External translation successful for Location {LocationId}", locationId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto-translation failed for Location {LocationId}. Error: {Message}.", locationId, ex.Message);
            
            // On failure, update status to 'failed' and abort audio queuing for non-Vietnamese
            foreach (var loc in touched)
            {
                if (!string.Equals(loc.LanguageCode, "vi-VN", StringComparison.OrdinalIgnoreCase))
                {
                    loc.LocalizedName = string.Empty;
                    loc.LocalizedDescription = string.Empty;
                    loc.AudioGenerationStatus = "failed";
                }
                else 
                {
                    loc.AudioGenerationStatus = "pending";
                    _audioQueue.TryEnqueue(loc.Id);
                }
                loc.UpdatedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        // 3. CẬP NHẬT LẠI KẾT QUẢ DỊCH VÀ ĐẨY GỌI TTS AUDIO
        foreach (var loc in touched)
        {
            if (!string.Equals(loc.LanguageCode, "vi-VN", StringComparison.OrdinalIgnoreCase))
            {
                if (translated.TryGetValue(loc.LanguageCode, out var val))
                {
                    loc.LocalizedName = val.LocalizedName;
                    loc.LocalizedDescription = val.LocalizedDescription;
                    loc.AudioGenerationStatus = "pending";
                    _audioQueue.TryEnqueue(loc.Id);
                }
                else 
                {
                    loc.AudioGenerationStatus = "failed";
                }
            }
            else 
            {
                loc.AudioGenerationStatus = "pending";
                _audioQueue.TryEnqueue(loc.Id);
            }
            loc.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Completed pack generation for Location {LocationId}", locationId);
    }

    private string GetDefaultVoice(string languageCode)
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
