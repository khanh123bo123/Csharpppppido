namespace TouristGuideWeb.Models;

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

    // Navigation property usage
    public Location? Location { get; set; }
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
