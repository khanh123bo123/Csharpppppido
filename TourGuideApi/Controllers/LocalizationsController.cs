using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TourGuideApi.Data;
using TourGuideApi.Models;
using TourGuideApi.Services;

namespace TourGuideApi.Controllers;

/// <summary>
/// Handles localization and multi-language content management.
/// Supports 5 languages: vi-VN, en-US, zh-CN, ja-JP, ko-KR
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LocalizationsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILocalizationTranslationService _translationService;
    private readonly IAudioGenerationQueue _audioQueue;
    private readonly ILogger<LocalizationsController> _logger;

    // Supported languages for the tour guide system
    private static readonly string[] SupportedLanguages = { "vi-VN", "en-US", "zh-CN", "ja-JP", "ko-KR" };

    public LocalizationsController(
        AppDbContext context,
        ILocalizationTranslationService translationService,
        IAudioGenerationQueue audioQueue,
        ILogger<LocalizationsController> logger)
    {
        _context = context;
        _translationService = translationService;
        _audioQueue = audioQueue;
        _logger = logger;
    }

    /// <summary>
    /// Get all localizations for a specific location
    /// GET: api/localizations/by-location/123
    /// </summary>
    [HttpGet("by-location/{locationId}")]
    public async Task<ActionResult<IEnumerable<LocalizationDto>>> GetLocalizationsByLocation(int locationId)
    {
        var localizations = await _context.Localizations
            .Where(l => l.LocationId == locationId)
            .Select(l => new LocalizationDto
            {
                Id = l.Id,
                LocationId = l.LocationId,
                LanguageCode = l.LanguageCode,
                LocalizedName = l.LocalizedName,
                LocalizedDescription = l.LocalizedDescription,
                CachedAudioUrl = l.CachedAudioUrl,
                TextToSpeechEndpoint = l.TextToSpeechEndpoint,
                AudioGenerationStatus = l.AudioGenerationStatus,
                TtsVoiceCode = l.TtsVoiceCode,
                IsWarmupProcessed = l.IsWarmupProcessed
            })
            .ToListAsync();

        if (!localizations.Any())
        {
            return NotFound($"No localizations found for location {locationId}");
        }

        return Ok(localizations);
    }

    /// <summary>
    /// Get localization for a specific location and language
    /// GET: api/localizations/123/vi-VN
    /// </summary>
    [HttpGet("{locationId}/{languageCode}")]
    public async Task<ActionResult<LocalizationDto>> GetLocalization(int locationId, string languageCode)
    {
        var localization = await _context.Localizations
            .Where(l => l.LocationId == locationId && l.LanguageCode == languageCode)
            .FirstOrDefaultAsync();

        if (localization == null)
        {
            return NotFound($"Localization not found for location {locationId} in language {languageCode}");
        }

        return Ok(new LocalizationDto
        {
            Id = localization.Id,
            LocationId = localization.LocationId,
            LanguageCode = localization.LanguageCode,
            LocalizedName = localization.LocalizedName,
            LocalizedDescription = localization.LocalizedDescription,
            CachedAudioUrl = localization.CachedAudioUrl,
            TextToSpeechEndpoint = localization.TextToSpeechEndpoint,
            AudioGenerationStatus = localization.AudioGenerationStatus,
            TtsVoiceCode = localization.TtsVoiceCode,
            IsWarmupProcessed = localization.IsWarmupProcessed
        });
    }

    /// <summary>
    /// Create or update a localization for a location
    /// POST: api/localizations
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<LocalizationDto>> CreateLocalization([FromBody] CreateLocalizationRequest request)
    {
        if (!SupportedLanguages.Contains(request.LanguageCode))
        {
            return BadRequest($"Language {request.LanguageCode} is not supported. Supported: {string.Join(", ", SupportedLanguages)}");
        }

        // Check if location exists
        var location = await _context.Locations.FindAsync(request.LocationId);
        if (location == null)
        {
            return NotFound($"Location {request.LocationId} not found");
        }

        // Check if localization already exists
        var existing = await _context.Localizations
            .FirstOrDefaultAsync(l => l.LocationId == request.LocationId && l.LanguageCode == request.LanguageCode);

        if (existing != null)
        {
            // Update existing
            existing.LocalizedName = request.LocalizedName;
            existing.LocalizedDescription = request.LocalizedDescription;
            existing.TtsVoiceCode = request.TtsVoiceCode;
            existing.UpdatedAt = DateTime.UtcNow;
            
            _context.Localizations.Update(existing);
            await _context.SaveChangesAsync();

            // Trigger TTS generation if audio is not cached
            if (string.IsNullOrWhiteSpace(existing.CachedAudioUrl))
            {
                QueueAudioGeneration(existing.Id);
            }

            return Ok(MapToDto(existing));
        }

        // Create new localization
        var localization = new Localization
        {
            LocationId = request.LocationId,
            LanguageCode = request.LanguageCode,
            LocalizedName = request.LocalizedName,
            LocalizedDescription = request.LocalizedDescription,
            TtsVoiceCode = request.TtsVoiceCode ?? GetDefaultVoice(request.LanguageCode),
            AudioGenerationStatus = "pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Localizations.Add(localization);
        await _context.SaveChangesAsync();

        // Trigger async TTS generation (TIER 2)
        QueueAudioGeneration(localization.Id);

        _logger.LogInformation($"Created localization for location {request.LocationId} in {request.LanguageCode}");
        return CreatedAtAction(nameof(GetLocalization), new { locationId = localization.LocationId, languageCode = localization.LanguageCode }, MapToDto(localization));
    }

    /// <summary>
    /// Create/update the FULL 5-language pack from a single Vietnamese input.
    /// Source is always Vietnamese (vi-VN). The API will translate into 4 remaining languages
    /// and queue TTS audio generation for all 5.
    /// POST: api/localizations/generate-pack
    /// </summary>
    [HttpPost("generate-pack")]
    public async Task<ActionResult<GenerateLocalizationPackResponse>> GenerateLocalizationPack(
        [FromBody] GenerateLocalizationPackRequest request,
        CancellationToken cancellationToken)
    {
        if (request.LocationId <= 0)
        {
            return BadRequest("LocationId is required.");
        }

        var vietnameseName = (request.VietnameseName ?? string.Empty).Trim();
        var vietnameseDescription = (request.VietnameseDescription ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(vietnameseName) || string.IsNullOrWhiteSpace(vietnameseDescription))
        {
            return BadRequest("VietnameseName and VietnameseDescription are required.");
        }

        var location = await _context.Locations.FindAsync(new object[] { request.LocationId }, cancellationToken);
        if (location == null)
        {
            return NotFound($"Location {request.LocationId} not found");
        }

        var targetLanguages = SupportedLanguages
            .Where(l => !string.Equals(l, "vi-VN", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Dictionary<string, LocalizedText> translated;
        try
        {
            translated = await _translationService.TranslateFromVietnameseAsync(
                vietnameseName,
                vietnameseDescription,
                targetLanguages,
                cancellationToken);
        }
        catch (LocalizationTranslationNotConfiguredException ex)
        {
            _logger.LogWarning(ex, "Localization pack translation is not configured.");
            return StatusCode(
                StatusCodes.Status501NotImplemented,
                new GenerateLocalizationPackResponse
                {
                    Status = "not_configured",
                    Message = ex.Message,
                    LocationId = request.LocationId,
                    Languages = SupportedLanguages
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to translate localization pack for location {LocationId}", request.LocationId);

            var message = ex is TaskCanceledException
                ? "Dịch tự động phản hồi quá chậm (timeout). Hãy thử lại sau hoặc kiểm tra Ollama đang chạy."
                : (ex.Message ?? "Dịch tự động thất bại. Hãy kiểm tra Ollama đang chạy và cấu hình Ollama:BaseUrl/Ollama:Model.");

            return StatusCode(
                StatusCodes.Status502BadGateway,
                new GenerateLocalizationPackResponse
                {
                    Status = "failed",
                    Message = message,
                    LocationId = request.LocationId,
                    Languages = SupportedLanguages
                });
        }

        var now = DateTime.UtcNow;
        var touched = new List<Localization>();

        foreach (var languageCode in SupportedLanguages)
        {
            var name = string.Equals(languageCode, "vi-VN", StringComparison.OrdinalIgnoreCase)
                ? vietnameseName
                : translated[languageCode].LocalizedName;

            var desc = string.Equals(languageCode, "vi-VN", StringComparison.OrdinalIgnoreCase)
                ? vietnameseDescription
                : translated[languageCode].LocalizedDescription;

            var existing = await _context.Localizations
                .FirstOrDefaultAsync(
                    l => l.LocationId == request.LocationId && l.LanguageCode == languageCode,
                    cancellationToken);

            if (existing == null)
            {
                var localization = new Localization
                {
                    LocationId = request.LocationId,
                    LanguageCode = languageCode,
                    LocalizedName = name,
                    LocalizedDescription = desc,
                    TtsVoiceCode = GetDefaultVoice(languageCode),
                    AudioGenerationStatus = "pending",
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.Localizations.Add(localization);
                touched.Add(localization);
                continue;
            }

            existing.LocalizedName = name;
            existing.LocalizedDescription = desc;
            existing.CachedAudioBase64 = null;
            existing.CachedAudioUrl = null;
            existing.AudioGenerationStatus = "pending";

            if (string.IsNullOrWhiteSpace(existing.TtsVoiceCode))
            {
                existing.TtsVoiceCode = GetDefaultVoice(languageCode);
            }

            existing.UpdatedAt = now;
            _context.Localizations.Update(existing);
            touched.Add(existing);
        }

        await _context.SaveChangesAsync(cancellationToken);

        foreach (var loc in touched)
        {
            QueueAudioGeneration(loc.Id);
        }

        return Ok(new GenerateLocalizationPackResponse
        {
            Status = "queued",
            Message = "Đã tạo/cập nhật 5 bản dịch. Audio đang được tạo ngầm.",
            LocationId = request.LocationId,
            Languages = SupportedLanguages
        });
    }

    /// <summary>
    /// Generate/regenerate audio for a localization (Manual trigger)
    /// POST: api/localizations/generate-audio
    /// </summary>
    [HttpPost("generate-audio")]
    public async Task<ActionResult<GenerateAudioResponse>> GenerateAudio([FromBody] GenerateAudioRequest request)
    {
        var localization = await _context.Localizations
            .FirstOrDefaultAsync(l => l.Id == request.LocalizationId);

        if (localization == null)
        {
            return NotFound($"Localization {request.LocalizationId} not found");
        }

        // Reset state and enqueue background generation
        localization.CachedAudioBase64 = null;
        localization.CachedAudioUrl = null;
        localization.AudioGenerationStatus = "pending";
        localization.UpdatedAt = DateTime.UtcNow;
        _context.Localizations.Update(localization);
        await _context.SaveChangesAsync();

        QueueAudioGeneration(localization.Id);
        return Ok(new GenerateAudioResponse { Status = "pending", Message = "Audio generation queued" });
    }

    /// <summary>
    /// Get cached audio file for a localization (TIER 1)
    /// GET: api/localizations/123/audio
    /// </summary>
    [HttpGet("{localizationId}/audio")]
    public async Task<IActionResult> GetAudio(int localizationId)
    {
        var localization = await _context.Localizations.FindAsync(localizationId);
        if (localization == null || string.IsNullOrWhiteSpace(localization.CachedAudioBase64))
        {
            return NotFound("Audio not found");
        }

        var audioBytes = Convert.FromBase64String(localization.CachedAudioBase64);
        return File(audioBytes, "audio/mpeg", $"poi_{localization.LocationId}_{localization.LanguageCode}.mp3");
    }

    /// <summary>
    /// Delete cached audio for a localization (free up DB storage)
    /// DELETE: api/localizations/123/audio
    /// </summary>
    [HttpDelete("{localizationId}/audio")]
    public async Task<IActionResult> DeleteAudio(int localizationId)
    {
        var localization = await _context.Localizations.FindAsync(localizationId);
        if (localization == null)
        {
            return NotFound();
        }

        localization.CachedAudioBase64 = null;
        localization.CachedAudioUrl = null;
        localization.AudioGenerationStatus = "deleted";
        localization.UpdatedAt = DateTime.UtcNow;

        _context.Localizations.Update(localization);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Delete a localization
    /// DELETE: api/localizations/123
    /// </summary>
    [HttpDelete("{localizationId}")]
    public async Task<IActionResult> DeleteLocalization(int localizationId)
    {
        var localization = await _context.Localizations.FindAsync(localizationId);
        if (localization == null)
        {
            return NotFound();
        }

        _context.Localizations.Remove(localization);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private LocalizationDto MapToDto(Localization localization)
    {
        return new LocalizationDto
        {
            Id = localization.Id,
            LocationId = localization.LocationId,
            LanguageCode = localization.LanguageCode,
            LocalizedName = localization.LocalizedName,
            LocalizedDescription = localization.LocalizedDescription,
            CachedAudioUrl = localization.CachedAudioUrl,
            TextToSpeechEndpoint = localization.TextToSpeechEndpoint,
            AudioGenerationStatus = localization.AudioGenerationStatus,
            TtsVoiceCode = localization.TtsVoiceCode,
            IsWarmupProcessed = localization.IsWarmupProcessed
        };
    }

    private void QueueAudioGeneration(int localizationId)
    {
        var enqueued = _audioQueue.TryEnqueue(localizationId);
        if (!enqueued)
        {
            _logger.LogDebug("Audio generation already queued for localization {LocalizationId}", localizationId);
        }
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

/// <summary>
/// DTOs for API requests and responses
/// </summary>
public class LocalizationDto
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

public class CreateLocalizationRequest
{
    public int LocationId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string LocalizedName { get; set; } = string.Empty;
    public string LocalizedDescription { get; set; } = string.Empty;
    public string? TtsVoiceCode { get; set; }
}

public class GenerateAudioRequest
{
    public int LocalizationId { get; set; }
}

public class GenerateAudioResponse
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class GenerateLocalizationPackRequest
{
    public int LocationId { get; set; }
    public string VietnameseName { get; set; } = string.Empty;
    public string VietnameseDescription { get; set; } = string.Empty;
}

public class GenerateLocalizationPackResponse
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int LocationId { get; set; }
    public string[] Languages { get; set; } = Array.Empty<string>();
}
