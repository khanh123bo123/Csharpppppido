using Android.OS;
using Android.Speech.Tts;
using Microsoft.Maui.ApplicationModel;

using AndroidTextToSpeech = Android.Speech.Tts.TextToSpeech;
using JavaLocale = Java.Util.Locale;

namespace TouristGuideApp.Platforms.Android;

internal static class OfflineTtsToFile
{
    public static async Task<bool> SynthesizeToWavAsync(string text, string languageCode, string outputPath, float speechRate = 1.0f)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        // Ensure folder exists
        try
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        catch
        {
            return false;
        }

        // Delete existing file if present
        try { if (System.IO.File.Exists(outputPath)) System.IO.File.Delete(outputPath); } catch { }

        var tcs = new TaskCompletionSource<bool>();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            AndroidTextToSpeech? tts = null;

            void finish(bool ok)
            {
                try { tts?.Stop(); } catch { }
                try { tts?.Shutdown(); } catch { }
                try { tts?.Dispose(); } catch { }
                tcs.TrySetResult(ok);
            }

            try
            {
                tts = new AndroidTextToSpeech(global::Android.App.Application.Context, new InitListener(status =>
                {
                    if (status != OperationResult.Success)
                    {
                        finish(false);
                        return;
                    }

                    try
                    {
                        var locale = CreateLocale(languageCode);
                        if (locale != null)
                        {
                            tts.SetLanguage(locale);
                        }

                        try
                        {
                            var clampedRate = Math.Clamp(speechRate, 0.1f, 4.0f);
                            tts.SetSpeechRate(clampedRate);
                        }
                        catch
                        {
                            // ignore speech rate failures
                        }

                        var utteranceId = Guid.NewGuid().ToString("N");

                        tts.SetOnUtteranceProgressListener(new ProgressListener(
                            onDone: id =>
                            {
                                if (id == utteranceId)
                                {
                                    finish(System.IO.File.Exists(outputPath));
                                }
                            },
                            onError: id =>
                            {
                                if (id == utteranceId)
                                {
                                    finish(false);
                                }
                            }));

                        var javaFile = new Java.IO.File(outputPath);

                        if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
                        {
                            var bundle = new Bundle();
                            tts.SynthesizeToFile(text, bundle, javaFile, utteranceId);
                        }
                        else
                        {
                            var legacyParams = new Dictionary<string, string>
                            {
                                { AndroidTextToSpeech.Engine.KeyParamUtteranceId, utteranceId }
                            };
                            tts.SynthesizeToFile(text, legacyParams, javaFile.AbsolutePath);
                        }
                    }
                    catch
                    {
                        finish(false);
                    }
                }));
            }
            catch
            {
                finish(false);
            }
        });

        // Hard timeout so we don't block the narration queue forever
        try
        {
            return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(60));
        }
        catch
        {
            return false;
        }
    }

    private static JavaLocale? CreateLocale(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode)) return null;

        var parts = languageCode.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            >= 2 => new JavaLocale(parts[0], parts[1]),
            1 => new JavaLocale(parts[0]),
            _ => null
        };
    }

    private sealed class InitListener : Java.Lang.Object, AndroidTextToSpeech.IOnInitListener
    {
        private readonly Action<OperationResult> _onInit;

        public InitListener(Action<OperationResult> onInit)
        {
            _onInit = onInit;
        }

        public void OnInit(OperationResult status)
        {
            _onInit(status);
        }
    }

    private sealed class ProgressListener : UtteranceProgressListener
    {
        private readonly Action<string> _onDone;
        private readonly Action<string> _onError;

        public ProgressListener(Action<string> onDone, Action<string> onError)
        {
            _onDone = onDone;
            _onError = onError;
        }

        public override void OnStart(string? utteranceId) { }

        public override void OnDone(string? utteranceId)
        {
            if (!string.IsNullOrWhiteSpace(utteranceId))
            {
                _onDone(utteranceId);
            }
        }

        public override void OnError(string? utteranceId)
        {
            if (!string.IsNullOrWhiteSpace(utteranceId))
            {
                _onError(utteranceId);
            }
        }
    }
}
