namespace TouristGuideWeb.Models;

public sealed class ApkDownloadViewModel
{
    public required string LandingUrl { get; init; }
    public required string DirectApkUrl { get; init; }
    public required string QrImageUrl { get; init; }
    public bool HasApk { get; init; }
    public string? LastUpdatedUtc { get; init; }
    public long SizeBytes { get; init; }
    public string? CurrentFileName { get; init; }
}
