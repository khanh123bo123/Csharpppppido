using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TourGuideApi.Services;

/// <summary>
/// Free (no API key) Tier-2 TTS using Microsoft Edge online voices via the open-source edge-tts CLI.
/// Produces MP3 bytes which are returned as base64.
///
/// Requirements:
/// - Python + edge-tts installed (pip install edge-tts)
/// - Internet access (edge-tts connects to Microsoft's TTS endpoint)
/// </summary>
public class EdgeTtsTextToSpeechService : ITextToSpeechService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EdgeTtsTextToSpeechService> _logger;

    // Edge-TTS can fail when invoked concurrently (multiple processes at once).
    // Serialize executions to keep pack generation stable.
    private static readonly SemaphoreSlim EdgeTtsConcurrencyGate = new(1, 1);

    // Voice mappings for the 5 key languages used in this project.
    private static readonly Dictionary<string, string> DefaultVoiceMapping = new()
    {
        { "vi-VN", "vi-VN-HoaiMyNeural" },
        { "en-US", "en-US-AriaNeural" },
        { "zh-CN", "zh-CN-XiaoxiaoNeural" },
        { "ja-JP", "ja-JP-NanamiNeural" },
        { "ko-KR", "ko-KR-SunHiNeural" }
    };

    public EdgeTtsTextToSpeechService(IConfiguration config, ILogger<EdgeTtsTextToSpeechService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<string> GenerateSpeechAsync(string text, string languageCode, string voiceCode)
    {
        if (string.IsNullOrWhiteSpace(text)) 
        {
            _logger.LogDebug("Empty text passed to TTS, returning empty result.");
            return string.Empty;
        }

        var edgeTtsExe = ResolveEdgeTtsExecutablePath();
        if (string.IsNullOrWhiteSpace(edgeTtsExe))
        {
            _logger.LogWarning("Edge-TTS executable not found. Install edge-tts (pip install edge-tts) or set EdgeTts:ExecutablePath.");
            return string.Empty;
        }

        var selectedVoice = string.IsNullOrWhiteSpace(voiceCode)
            ? DefaultVoiceMapping.GetValueOrDefault(languageCode, "en-US-AriaNeural")
            : voiceCode;

        if (string.Equals(selectedVoice, "ja-JP-NanamiNeural", StringComparison.OrdinalIgnoreCase))
        {
            // Already correct, but keeping structure if we need other fixes
            selectedVoice = "ja-JP-NanamiNeural";
        }

        var timeoutSeconds = _config.GetValue<int?>("EdgeTts:TimeoutSeconds") ?? 90;
        var rateArg = ResolveRateArgument();
        var outputPath = Path.Combine(Path.GetTempPath(), $"edge_tts_{Guid.NewGuid():N}.mp3");
        var textFilePath = Path.Combine(Path.GetTempPath(), $"edge_tts_{Guid.NewGuid():N}.txt");

        var gateAcquired = false;

        try
        {
            await EdgeTtsConcurrencyGate.WaitAsync();
            gateAcquired = true;
            await File.WriteAllTextAsync(textFilePath, text);

            var startInfo = new ProcessStartInfo
            {
                FileName = edgeTtsExe,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Use --file to avoid command-line quoting/length issues.
            startInfo.ArgumentList.Add("--file");
            startInfo.ArgumentList.Add(textFilePath);
            startInfo.ArgumentList.Add("--voice");
            startInfo.ArgumentList.Add(selectedVoice);
            startInfo.ArgumentList.Add("--write-media");
            startInfo.ArgumentList.Add(outputPath);

            if (!string.IsNullOrWhiteSpace(rateArg))
            {
                startInfo.ArgumentList.Add($"--rate={rateArg}");
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                _logger.LogWarning("Failed to start edge-tts process.");
                return string.Empty;
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                _logger.LogWarning("Edge-TTS timed out after {TimeoutSeconds}s.", timeoutSeconds);
                return string.Empty;
            }

            var stderr = (await stderrTask).Trim();
            var stdout = (await stdoutTask).Trim();

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("Edge-TTS failed (exit {ExitCode}). StdErr: {StdErr}. StdOut: {StdOut}", process.ExitCode, stderr, stdout);
                return string.Empty;
            }

            if (!File.Exists(outputPath))
            {
                _logger.LogWarning("Edge-TTS completed but output file is missing: {OutputPath}. StdErr: {StdErr}", outputPath, stderr);
                return string.Empty;
            }

            var audioBytes = await File.ReadAllBytesAsync(outputPath);
            if (audioBytes.Length == 0)
            {
                _logger.LogWarning("Edge-TTS generated an empty file for text: {Text}", text.Length > 20 ? text[..20] + "..." : text);
                return string.Empty;
            }
            return Convert.ToBase64String(audioBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Edge-TTS generation failed.");
            return string.Empty;
        }
        finally
        {
            if (gateAcquired)
            {
                EdgeTtsConcurrencyGate.Release();
            }
            SafeDeleteFile(outputPath);
            SafeDeleteFile(textFilePath);
        }
    }

    public Task WarmupLocalizationsAsync(int locationId)
    {
        // Optional: could be implemented to bulk-generate audio for all localizations.
        return Task.CompletedTask;
    }

    public Task<string[]> GetAvailableVoicesAsync(string languageCode)
    {
        // Keep simple and predictable. The controller already uses these defaults.
        var voices = DefaultVoiceMapping.Values.Distinct().ToArray();
        return Task.FromResult(voices);
    }

    public Task<string> SynthesizeAsync(string text, string fileName)
    {
        // Legacy compatibility: keep using Vietnamese default.
        return GenerateSpeechAsync(text, "vi-VN", string.Empty);
    }

    private string? ResolveEdgeTtsExecutablePath()
    {
        var configured = _config["EdgeTts:ExecutablePath"];
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        var fromPath = TryFindOnPath(OperatingSystem.IsWindows() ? "edge-tts.exe" : "edge-tts");
        if (!string.IsNullOrWhiteSpace(fromPath))
        {
            return fromPath;
        }

        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var pythonRoot = Path.Combine(localAppData, "Programs", "Python");
            if (Directory.Exists(pythonRoot))
            {
                var pythonDirs = Directory.EnumerateDirectories(pythonRoot, "Python*", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase);

                foreach (var pythonDir in pythonDirs)
                {
                    var candidate = Path.Combine(pythonDir, "Scripts", "edge-tts.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        return null;
    }

    private string? ResolveRateArgument()
    {
        // Option A: direct edge-tts rate string, e.g. "+0%", "-75%".
        var configuredRate = (_config["EdgeTts:Rate"] ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(configuredRate))
        {
            return configuredRate;
        }

        // Option B: multiplier (x1.0 = normal, x0.25 = very slow).
        var speechRate = _config.GetValue<double?>("EdgeTts:SpeechRate");
        if (speechRate is null)
        {
            return null;
        }

        var multiplier = speechRate.Value;
        if (double.IsNaN(multiplier) || double.IsInfinity(multiplier))
        {
            return null;
        }

        // Clamp to a reasonable range before converting to percent.
        multiplier = Math.Clamp(multiplier, 0.1, 4.0);

        var percent = (multiplier - 1.0) * 100.0;
        var rounded = (int)Math.Round(percent, MidpointRounding.AwayFromZero);
        return rounded >= 0 ? $"+{rounded}%" : $"{rounded}%";
    }

    private static string? TryFindOnPath(string executableName)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var segment in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(segment.Trim(), executableName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // ignore malformed PATH entries
            }
        }

        return null;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // ignored
        }
    }

    private static void SafeDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignored
        }
    }
}
