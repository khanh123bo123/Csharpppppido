using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TourGuideApi.Models;

/// <summary>
/// Stores localized content for locations with support for multiple languages.
/// Supports the 4-Tier Hybrid Audio system (Cache, API TTS, Offline TTS, Browser TTS).
/// Languages: vi-VN, en-US, zh-CN, ja-JP, ko-KR
/// </summary>
public class Localization
{
    [Key]
    public int Id { get; set; }

    [Required]
    [ForeignKey(nameof(Location))]
    public int LocationId { get; set; }

    [Required]
    public Location Location { get; set; } = null!;

    /// <summary>
    /// ISO 639 language code (vi-VN, en-US, zh-CN, ja-JP, ko-KR)
    /// </summary>
    [Required]
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// Localized name of the location (e.g., "Quán Hủ Tiếu Nam Vang" vs "Hu Tieu Su Nam Vang")
    /// </summary>
    [Required]
    public string LocalizedName { get; set; } = string.Empty;

    /// <summary>
    /// Localized description/narrative of the location
    /// </summary>
    [Required]
    public string LocalizedDescription { get; set; } = string.Empty;

    /// <summary>
    /// TIER 1: Pre-generated MP3 audio file cached locally (stored as base64 or file path)
    /// Size: ~50-200KB per location per language (compressed MP3)
    /// </summary>
    public string? CachedAudioBase64 { get; set; }

    /// <summary>
    /// URL/Path to cached audio file on device (TIER 1)
    /// Example: "documents://poi_123_vi.mp3"
    /// </summary>
    public string? CachedAudioUrl { get; set; }

    /// <summary>
    /// TIER 2: TTS-generated audio endpoint (server-generated)
    /// Fallback if cache is missing
    /// </summary>
    public string? TextToSpeechEndpoint { get; set; }

    /// <summary>
    /// Status of audio generation for 4-tier system
    /// Values: "pending" | "generated" | "cached" | "failed"
    /// </summary>
    public string AudioGenerationStatus { get; set; } = "pending";

    /// <summary>
    /// Edge-TTS or Offline TTS voice code (for TIER 2-3 fallback)
    /// Example: "vi-VN-HoaiMyNeural" (Microsoft), "ja-JP-NanamiNeural"
    /// </summary>
    public string? TtsVoiceCode { get; set; }

    /// <summary>
    /// When this localization was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this localization was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this translation is marked as "warmup processed"
    /// (i.e., TTS pre-generated when location was first created)
    /// </summary>
    public bool IsWarmupProcessed { get; set; } = false;

    /// <summary>
    /// QR code data specific to this language (optional)
    /// Can be used for multilingual QR code scanning
    /// </summary>
    public string? QrCodeData { get; set; }

    /// <summary>
    /// Total number of times this audio has been played
    /// </summary>
    public int PlayCount { get; set; } = 0;
}
