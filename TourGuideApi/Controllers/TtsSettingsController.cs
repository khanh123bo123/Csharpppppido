using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;

namespace TourGuideApi.Controllers;

/// <summary>
/// Runtime settings for Text-to-Speech (Edge-TTS) to avoid editing JSON manually.
/// Writes overrides into appsettings.Local.json (gitignored) so settings persist.
/// </summary>
[ApiController]
[Route("api/settings")]
public sealed class TtsSettingsController : ControllerBase
{
    private static readonly SemaphoreSlim SettingsFileGate = new(1, 1);

    private readonly IHostEnvironment _hostEnvironment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TtsSettingsController> _logger;

    public TtsSettingsController(
        IHostEnvironment hostEnvironment,
        IConfiguration configuration,
        ILogger<TtsSettingsController> logger)
    {
        _hostEnvironment = hostEnvironment;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("edge-tts")]
    public ActionResult<EdgeTtsSettingsDto> GetEdgeTtsSettings()
    {
        return Ok(new EdgeTtsSettingsDto
        {
            ExecutablePath = (_configuration["EdgeTts:ExecutablePath"] ?? string.Empty).Trim(),
            TimeoutSeconds = _configuration.GetValue<int?>("EdgeTts:TimeoutSeconds"),
            SpeechRate = _configuration.GetValue<double?>("EdgeTts:SpeechRate"),
            Rate = (_configuration["EdgeTts:Rate"] ?? string.Empty).Trim()
        });
    }

    [HttpPost("edge-tts")]
    public async Task<ActionResult<EdgeTtsSettingsDto>> UpdateEdgeTtsSettings(
        [FromBody] UpdateEdgeTtsSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("Missing request body.");
        }

        var hasSpeechRate = request.SpeechRate is not null;
        var hasRate = !string.IsNullOrWhiteSpace(request.Rate);

        if (hasSpeechRate && hasRate)
        {
            return BadRequest("Provide either speechRate or rate (not both).");
        }

        if (!hasSpeechRate && !hasRate)
        {
            return BadRequest("Provide speechRate or rate.");
        }

        double? normalizedSpeechRate = null;
        if (request.SpeechRate is double speechRate)
        {
            if (double.IsNaN(speechRate) || double.IsInfinity(speechRate))
            {
                return BadRequest("speechRate is invalid.");
            }

            // Android TTS & Edge-TTS both behave well in this range.
            normalizedSpeechRate = Math.Clamp(speechRate, 0.1, 4.0);
            normalizedSpeechRate = Math.Round(normalizedSpeechRate.Value, 2, MidpointRounding.AwayFromZero);
        }

        string? normalizedRate = null;
        if (hasRate)
        {
            normalizedRate = request.Rate!.Trim();

            // Basic validation: edge-tts expects something like +0%, -75%, 10%.
            if (!System.Text.RegularExpressions.Regex.IsMatch(normalizedRate, "^[+-]?\\d+%$"))
            {
                return BadRequest("rate must look like '+0%', '-75%', or '10%'.");
            }
        }

        var settingsFilePath = Path.Combine(_hostEnvironment.ContentRootPath, "appsettings.Local.json");

        await SettingsFileGate.WaitAsync(cancellationToken);
        try
        {
            var root = await LoadOrCreateJsonAsync(settingsFilePath, cancellationToken);
            var edgeTts = root["EdgeTts"] as JsonObject ?? new JsonObject();

            if (normalizedSpeechRate is not null)
            {
                edgeTts["SpeechRate"] = normalizedSpeechRate.Value;
                // Explicitly clear Rate in case it's set by other config providers.
                edgeTts["Rate"] = string.Empty;
            }

            if (normalizedRate is not null)
            {
                edgeTts["Rate"] = normalizedRate;
                edgeTts.Remove("SpeechRate");
            }

            root["EdgeTts"] = edgeTts;

            await SaveJsonAtomicAsync(settingsFilePath, root, cancellationToken);

            _logger.LogInformation(
                "Updated Edge-TTS settings via API. SpeechRate={SpeechRate}, Rate={Rate}",
                normalizedSpeechRate,
                normalizedRate);
        }
        finally
        {
            SettingsFileGate.Release();
        }

        // Return the updated values immediately (configuration reload is async).
        var response = new EdgeTtsSettingsDto
        {
            ExecutablePath = (_configuration["EdgeTts:ExecutablePath"] ?? string.Empty).Trim(),
            TimeoutSeconds = _configuration.GetValue<int?>("EdgeTts:TimeoutSeconds"),
            SpeechRate = _configuration.GetValue<double?>("EdgeTts:SpeechRate"),
            Rate = (_configuration["EdgeTts:Rate"] ?? string.Empty).Trim()
        };

        if (normalizedSpeechRate is not null)
        {
            response.SpeechRate = normalizedSpeechRate;
            response.Rate = string.Empty;
        }

        if (normalizedRate is not null)
        {
            response.Rate = normalizedRate;
        }

        return Ok(response);
    }

    private static async Task<JsonObject> LoadOrCreateJsonAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!System.IO.File.Exists(filePath))
        {
            return new JsonObject();
        }

        try
        {
            var text = await System.IO.File.ReadAllTextAsync(filePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new JsonObject();
            }

            var node = JsonNode.Parse(text);
            return node as JsonObject ?? new JsonObject();
        }
        catch
        {
            // If malformed, reset to an empty object rather than failing.
            return new JsonObject();
        }
    }

    private static async Task SaveJsonAtomicAsync(string filePath, JsonObject root, CancellationToken cancellationToken)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = root.ToJsonString(options);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tmpPath = filePath + ".tmp";
        await System.IO.File.WriteAllTextAsync(tmpPath, json, cancellationToken);

        // Replace existing atomically where possible.
        if (System.IO.File.Exists(filePath))
        {
            var backupPath = filePath + ".bak";
            System.IO.File.Replace(tmpPath, filePath, backupPath, ignoreMetadataErrors: true);
            try
            {
                System.IO.File.Delete(backupPath);
            }
            catch
            {
                // ignore
            }
            return;
        }

        System.IO.File.Move(tmpPath, filePath);
    }

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
}
