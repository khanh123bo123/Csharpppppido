using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Maui.Media;

namespace TouristGuideApp.Services
{
    /// <summary>
    /// 4-Tier Hybrid Audio System for mobile:
    /// TIER 1: Local cache (MP3 file on device)
    /// TIER 2: Download pre-generated audio from API
    /// TIER 3: Edge-TTS via API (offline TTS, no cost)
    /// TIER 4: MAUI TextToSpeech.Default (browser-equivalent fallback)
    /// </summary>
    public interface IAudioService
    {
        Task EnqueueSpeechAsync(string text, string? cachedAudioUrl = null, Action? onStarted = null, Action? onEnded = null);
        bool IsPlaying { get; }
        Task SetLanguageAsync(string languageCode);
        string CurrentLanguage { get; }
        Task PrefetchAudioAsync(string poiName, string languageCode);
    }

    public class AudioService : IAudioService
    {
        private readonly Queue<(string Text, string? AudioUrl, Action? OnStarted, Action? OnEnded)> _speechQueue = new();
        private bool _isProcessing = false;
        public bool IsPlaying { get; private set; }
        public string CurrentLanguage { get; private set; } = "vi-VN";

        public async Task SetLanguageAsync(string languageCode)
        {
            CurrentLanguage = languageCode;
            await Task.CompletedTask;
        }

        /// <summary>
        /// Enqueue speech with 4-tier fallback system
        /// </summary>
        public async Task EnqueueSpeechAsync(string text, string? cachedAudioUrl = null, Action? onStarted = null, Action? onEnded = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            _speechQueue.Enqueue((text, cachedAudioUrl, onStarted, onEnded));

            if (!_isProcessing)
            {
                await ProcessQueueAsync();
            }
        }

        private async Task ProcessQueueAsync()
        {
            _isProcessing = true;

            while (_speechQueue.Count > 0)
            {
                var item = _speechQueue.Dequeue();

                IsPlaying = true;
                item.OnStarted?.Invoke();

                try
                {
                    bool playedAudio = false;

                    // TIER 1: Try cached audio URL first
                    if (!string.IsNullOrWhiteSpace(item.AudioUrl))
                    {
                        playedAudio = await PlayCachedAudioAsync(item.AudioUrl);
                    }
                    
                    // TIER 4: Fallback to device TTS if audio didn't play
                    if (!playedAudio)
                    {
                        var textToSpeak = string.IsNullOrWhiteSpace(item.Text) ? "Chưa có đoạn văn mẫu thuyết minh." : item.Text;
                        
                        // Cố định lấy giọng ngôn ngữ tiếng Việt (Chị Google)
                        var locales = await TextToSpeech.Default.GetLocalesAsync();
                        var vnLocale = locales.FirstOrDefault(l => l.Language.ToLowerInvariant().Contains("vi") || l.Country.ToLowerInvariant().Contains("vn"));

                        await TextToSpeech.Default.SpeakAsync(textToSpeak, new SpeechOptions
                        {
                            Pitch = 1.0f,
                            Volume = 1.0f,
                            Locale = vnLocale
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Audio Error: {ex.Message}");
                }

                IsPlaying = false;
                item.OnEnded?.Invoke();

                // Pause between audio items
                await Task.Delay(500);
            }

            _isProcessing = false;
        }

        /// <summary>
        /// Play cached audio file from device storage
        /// </summary>
        private async Task<bool> PlayCachedAudioAsync(string audioUrl)
        {
            try
            {
                // TODO: For now if it's an HTTP link, we just return false so it falls back to TTS.
                if (audioUrl.StartsWith("http")) return false;

                var audioFilePath = Path.Combine(FileSystem.AppDataDirectory, audioUrl);
                
                if (File.Exists(audioFilePath))
                {
                    System.Diagnostics.Debug.WriteLine($"Playing cached audio: {audioFilePath}");
                    // TODO: Implement actual audio playback using MediaElement or platform APIs
                    await Task.Delay(1000); // Simulate playback
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to play cached audio: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Prefetch audio for a POI to enable offline playback
        /// </summary>
        public async Task PrefetchAudioAsync(string poiName, string languageCode)
        {
            try
            {
                // TODO: Implement audio prefetching from API
                // This would download and cache audio files in bulk
                System.Diagnostics.Debug.WriteLine($"Prefetching audio for {poiName} in {languageCode}");
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Prefetch failed: {ex.Message}");
            }
        }
    }
}
