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
    private readonly ITextToSpeechService _textToSpeechService;
    private readonly ILogger<LocalizationsController> _logger;

    // Supported languages for the tour guide system
    private static readonly string[] SupportedLanguages = { "vi-VN", "en-US", "zh-CN", "ja-JP", "ko-KR" };

    public LocalizationsController(
        AppDbContext context,
        ITextToSpeechService textToSpeechService,
        ILogger<LocalizationsController> logger)
    {
        _context = context;
        _textToSpeechService = textToSpeechService;
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
                _ = GenerateAudioAsync(existing);
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

        // Trigger async TTS generation (TIER 2 or 3)
        _ = GenerateAudioAsync(localization);

        _logger.LogInformation($"Created localization for location {request.LocationId} in {request.LanguageCode}");
        return CreatedAtAction(nameof(GetLocalization), new { locationId = localization.LocationId, languageCode = localization.LanguageCode }, MapToDto(localization));
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

        localization.AudioGenerationStatus = "pending";
        _context.Localizations.Update(localization);
        await _context.SaveChangesAsync();

        // Trigger async generation
        var audioBase64 = await _textToSpeechService.GenerateSpeechAsync(
            localization.LocalizedDescription,
            localization.LanguageCode,
            localization.TtsVoiceCode ?? GetDefaultVoice(localization.LanguageCode));

        if (!string.IsNullOrWhiteSpace(audioBase64))
        {
            localization.CachedAudioBase64 = audioBase64;
            localization.AudioGenerationStatus = audioBase64.Contains("EDGE_TTS") ? "pending" : "generated";
            localization.CachedAudioUrl = $"/api/localizations/{localization.Id}/audio";
            localization.UpdatedAt = DateTime.UtcNow;
            _context.Localizations.Update(localization);
            await _context.SaveChangesAsync();
        }

        return Ok(new GenerateAudioResponse { Status = localization.AudioGenerationStatus, Message = "Audio generation started" });
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

    private async Task GenerateAudioAsync(Localization localization)
    {
        try
        {
            var voiceCode = localization.TtsVoiceCode ?? GetDefaultVoice(localization.LanguageCode);
            var audioBase64 = await _textToSpeechService.GenerateSpeechAsync(
                localization.LocalizedDescription,
                localization.LanguageCode,
                voiceCode);

            if (!string.IsNullOrWhiteSpace(audioBase64) && !audioBase64.Contains("EDGE_TTS"))
            {
                localization.CachedAudioBase64 = audioBase64;
                localization.AudioGenerationStatus = "generated";
                localization.CachedAudioUrl = $"/api/localizations/{localization.Id}/audio";
                localization.UpdatedAt = DateTime.UtcNow;
                _context.Localizations.Update(localization);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Audio generated for localization {localization.Id}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to generate audio for localization {localization.Id}: {ex.Message}");
            localization.AudioGenerationStatus = "failed";
            _context.Localizations.Update(localization);
            await _context.SaveChangesAsync();
        }
    }

    private string GetDefaultVoice(string languageCode)
    {
        return languageCode switch
        {
            "vi-VN" => "vi-VN-HoaiMyNeural",
            "en-US" => "en-US-AriaNeural",
            "zh-CN" => "zh-CN-XiaoxiaoNeural",
            "ja-JP" => "ja-JP-NanomiNeural",
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
