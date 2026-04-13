namespace TouristGuideWeb.Models;

public sealed class EdgeTtsSettingsDto
{
    public string ExecutablePath { get; set; } = string.Empty;
    public int? TimeoutSeconds { get; set; }
    public double? SpeechRate { get; set; }
    public string Rate { get; set; } = string.Empty;
}

public sealed class UpdateEdgeTtsSettingsRequest
{
    public double? SpeechRate { get; set; }
    public string? Rate { get; set; }
}
