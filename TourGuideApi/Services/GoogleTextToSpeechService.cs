using Google.Cloud.TextToSpeech.V1;
using System.Text;

namespace TourGuideApi.Services;

public class GoogleTextToSpeechService : ITextToSpeechService
{
    private readonly IWebHostEnvironment _environment;

    public GoogleTextToSpeechService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> SynthesizeAsync(string text, string fileName)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text is required.", nameof(text));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name is required.", nameof(fileName));
        }

        var credentialsPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
        if (string.IsNullOrWhiteSpace(credentialsPath) || !File.Exists(credentialsPath))
        {
            throw new InvalidOperationException("GOOGLE_APPLICATION_CREDENTIALS is not set or points to a missing file.");
        }

        var client = await TextToSpeechClient.CreateAsync();

        var safeName = GetSafeFileName(fileName);
        var wwwrootPath = string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot")
            : _environment.WebRootPath;

        var audioDirectory = Path.Combine(wwwrootPath, "audio");
        Directory.CreateDirectory(audioDirectory);

        var outputPath = Path.Combine(audioDirectory, $"{safeName}.mp3");

        var response = await client.SynthesizeSpeechAsync(new SynthesizeSpeechRequest
        {
            Input = new SynthesisInput
            {
                Text = text
            },
            Voice = new VoiceSelectionParams
            {
                LanguageCode = "vi-VN",
                SsmlGender = SsmlVoiceGender.Neutral
            },
            AudioConfig = new AudioConfig
            {
                AudioEncoding = AudioEncoding.Mp3
            }
        });

        await File.WriteAllBytesAsync(outputPath, response.AudioContent.ToByteArray());

        return $"/audio/{safeName}.mp3";
    }

    private static string GetSafeFileName(string fileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName).Trim();
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = Guid.NewGuid().ToString("N");
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(baseName.Length);

        foreach (var ch in baseName)
        {
            builder.Append(invalidChars.Contains(ch) ? '_' : ch);
        }

        var cleaned = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? Guid.NewGuid().ToString("N") : cleaned;
    }
}
