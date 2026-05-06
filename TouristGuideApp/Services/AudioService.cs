using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Media;
using Microsoft.Maui.Networking;

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
        Task EnqueueSpeechAsync(string text, int? serverLocationId = null, string? cachedAudioUrl = null, bool forceOfflineTts = false, Action? onStarted = null, Action? onEnded = null);
        Task StopAsync();
        bool IsPlaying { get; }
        Task SetLanguageAsync(string languageCode);
        string CurrentLanguage { get; }
        Task PrefetchAudioAsync(string poiName, string languageCode);
    }

    public class AudioService : IAudioService
    {
        private readonly IApiService _apiService;
        private readonly Queue<(string Text, int? ServerLocationId, string? AudioUrl, bool ForceOfflineTts, Action? OnStarted, Action? OnEnded)> _speechQueue = new();
        private bool _isProcessing = false;
        private CancellationTokenSource? _currentCts;
        public bool IsPlaying { get; private set; }
        public string CurrentLanguage { get; private set; } = "vi-VN";

        public AudioService(IApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task StopAsync()
        {
            _speechQueue.Clear();
            _currentCts?.Cancel();
            _currentCts = new CancellationTokenSource(); // Reset CTS
            
            try 
            {
                // Note: MAUI TextToSpeech doesn't have a direct 'Stop', 
                // but calling SpeakAsync with empty string is a common workaround.
                // We run it without awaiting to avoid blocking.
                _ = TextToSpeech.Default.SpeakAsync(""); 
            } catch { }
            
            IsPlaying = false;
            _isProcessing = false;
            await Task.CompletedTask;
        }

        public async Task SetLanguageAsync(string languageCode)
        {
            CurrentLanguage = languageCode;
            await Task.CompletedTask;
        }

        /// <summary>
        /// Enqueue speech with 4-tier fallback system
        /// </summary>
        public async Task EnqueueSpeechAsync(string text, int? serverLocationId = null, string? cachedAudioUrl = null, bool forceOfflineTts = false, Action? onStarted = null, Action? onEnded = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            // Optional: If we want "Play immediately" behavior, uncomment below:
            // await StopAsync();

            _speechQueue.Enqueue((text, serverLocationId, cachedAudioUrl, forceOfflineTts, onStarted, onEnded));

            if (!_isProcessing)
            {
                await ProcessQueueAsync();
            }
        }

        private async Task ProcessQueueAsync()
        {
            _isProcessing = true;
            _currentCts = new CancellationTokenSource();

            while (_speechQueue.Count > 0 && !_currentCts.IsCancellationRequested)
            {
                var item = _speechQueue.Dequeue();

                IsPlaying = true;
                item.OnStarted?.Invoke();

                // REPORT: Log the listen event to the server if online
                if (item.ServerLocationId is > 0)
                {
                    _ = _apiService.LogListenAsync(item.ServerLocationId.Value, CurrentLanguage);
                }

                try
                {
                    if (_currentCts.IsCancellationRequested) break;
                    
                    bool playedAudio = false;

                    if (!item.ForceOfflineTts)
                    {
                        // TIER 1/2: Prefer location-based cache + API download when available
                        if (item.ServerLocationId is > 0)
                        {
                            playedAudio = await TryPlayFromCacheOrApiAsync(item.ServerLocationId.Value, item.Text);
                        }

                        // Backward compatibility: if caller provided a local cache key/path
                        // IMPORTANT: We only play the provided AudioUrl if it's either local OR matching Vietnamese
                        // If we are in English mode, we don't want to play the default Vietnamese remote AudioUrl.
                        if (!playedAudio && !string.IsNullOrWhiteSpace(item.AudioUrl))
                        {
                            bool isDefaultRemoteAudio = item.AudioUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase);
                            if (!isDefaultRemoteAudio || string.Equals(CurrentLanguage, "vi-VN", StringComparison.OrdinalIgnoreCase))
                            {
                                playedAudio = await TryPlayLocalCachedAudioAsync(item.AudioUrl);
                            }
                        }
                    }
                    
                    // TIER 4: Fallback to device TTS if audio didn't play
                    if (!playedAudio)
                    {
                        await SpeakFallbackAsync(item.Text);
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
        /// TIER 1/2:
        /// - Play cached MP3/WAV from device if present
        /// - If online, download generated MP3 from API and cache it
        /// - If offline, try to synthesize to a local file (Android) and cache it
        /// </summary>
        private async Task<bool> TryPlayFromCacheOrApiAsync(int serverLocationId, string text)
        {
            try
            {
                EnsureCacheFolderExists();

                // TIER 1: local cache (downloaded MP3 or offline WAV)
                var existing = FindExistingCachedFile(serverLocationId, CurrentLanguage);
                if (!string.IsNullOrWhiteSpace(existing))
                {
                    return await PlayFileAsync(existing);
                }

                // TIER 2: download pre-generated MP3 from API when online
                if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                {
                    var localization = await _apiService.GetLocalizationAsync(serverLocationId, CurrentLanguage);
                    if (localization != null && localization.Id > 0)
                    {
                        if (string.Equals(localization.AudioGenerationStatus, "generated", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(localization.AudioGenerationStatus, "cached", StringComparison.OrdinalIgnoreCase))
                        {
                            var audioBytes = await _apiService.GetLocalizationAudioBytesAsync(localization.Id);
                            if (audioBytes != null && audioBytes.Length > 0)
                            {
                                var mp3Path = GetOnlineMp3CachePath(serverLocationId, CurrentLanguage);
                                await File.WriteAllBytesAsync(mp3Path, audioBytes);
                                return await PlayFileAsync(mp3Path);
                            }
                        }
                    }
                }

                // OFFLINE: try synthesize-to-file (Android), cache for next time
                var wavPath = GetOfflineWavCachePath(serverLocationId, CurrentLanguage);
                var synthesized = await TrySynthesizeOfflineToFileAsync(text, CurrentLanguage, wavPath);
                if (synthesized && File.Exists(wavPath))
                {
                    return await PlayFileAsync(wavPath);
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to play/cache audio: {ex.Message}");
                return false;
            }
        }

        private static void EnsureCacheFolderExists()
        {
            var dir = Path.Combine(FileSystem.AppDataDirectory, "tts_cache");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        private static string? FindExistingCachedFile(int serverLocationId, string languageCode)
        {
            var mp3Path = GetOnlineMp3CachePath(serverLocationId, languageCode);
            if (File.Exists(mp3Path)) return mp3Path;

            var wavPath = GetOfflineWavCachePath(serverLocationId, languageCode);
            if (File.Exists(wavPath)) return wavPath;

            return null;
        }

        private static string GetOnlineMp3CachePath(int serverLocationId, string languageCode)
        {
            var safeLang = (languageCode ?? "vi-VN").Replace('/', '_').Replace('\\', '_');
            return Path.Combine(FileSystem.AppDataDirectory, "tts_cache", $"poi_{serverLocationId}_{safeLang}.mp3");
        }

        private static string GetOfflineWavCachePath(int serverLocationId, string languageCode)
        {
            var safeLang = (languageCode ?? "vi-VN").Replace('/', '_').Replace('\\', '_');
            return Path.Combine(FileSystem.AppDataDirectory, "tts_cache", $"poi_{serverLocationId}_{safeLang}.wav");
        }

        private static async Task<bool> TryPlayLocalCachedAudioAsync(string audioUrl)
        {
            try
            {
                // Keep original behavior: treat provided value as relative file key under AppData
                if (audioUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var audioFilePath = Path.Combine(FileSystem.AppDataDirectory, audioUrl);
                if (!File.Exists(audioFilePath))
                {
                    return false;
                }

                return await PlayFileAsync(audioFilePath);
            }
            catch
            {
                return false;
            }
        }

        private async Task SpeakFallbackAsync(string text)
        {
            var textToSpeak = string.IsNullOrWhiteSpace(text) ? "Chưa có đoạn văn mẫu thuyết minh." : text;

            try
            {
                var locales = await TextToSpeech.Default.GetLocalesAsync();
                
                // AUTO-DETECTION: If the text is clearly Vietnamese but the app is in another language, 
                // we MUST use a Vietnamese voice to avoid 'foreigner accent' reading Vietnamese text.
                string languageToUse = CurrentLanguage;
                if (!string.Equals(CurrentLanguage, "vi-VN", StringComparison.OrdinalIgnoreCase))
                {
                    if (IsVietnameseText(textToSpeak))
                    {
                        System.Diagnostics.Debug.WriteLine("[AudioService] Detected Vietnamese text in non-Vietnamese mode. Forcing vi-VN voice.");
                        languageToUse = "vi-VN";
                    }
                }

                var target = FindBestLocale(locales, languageToUse);

                if (target == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[TTS] No perfect match for {languageToUse}. Trying generic language match.");
                    var langOnly = languageToUse.Split('-')[0];
                    target = locales?.FirstOrDefault(l => string.Equals(l.Language, langOnly, StringComparison.OrdinalIgnoreCase));
                }

                await TextToSpeech.Default.SpeakAsync(textToSpeak, new SpeechOptions
                {
                    Pitch = 1.0f,
                    Volume = 1.0f,
                    Locale = target
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TTS ERROR]: {ex.Message}");
                await TextToSpeech.Default.SpeakAsync(textToSpeak);
            }
        }

        private static bool IsVietnameseText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            // Common Vietnamese-only characters
            char[] viChars = { 'đ', 'Đ', 'á', 'à', 'ả', 'ã', 'ạ', 'ă', 'ắ', 'ằ', 'ẳ', 'ẵ', 'ặ', 'â', 'ấ', 'ầ', 'ẩ', 'ẫ', 'ậ', 'é', 'è', 'ẻ', 'ẽ', 'ẹ', 'ê', 'ế', 'ề', 'ể', 'ễ', 'ệ', 'í', 'ì', 'ỉ', 'ĩ', 'ị', 'ó', 'ò', 'ỏ', 'õ', 'ọ', 'ô', 'ố', 'ồ', 'ổ', 'ỗ', 'ộ', 'ơ', 'ớ', 'ờ', 'ở', 'ỡ', 'ợ', 'ú', 'ù', 'ủ', 'ũ', 'ụ', 'ư', 'ứ', 'ừ', 'ử', 'ữ', 'ự', 'ý', 'ỳ', 'ỷ', 'ỹ', 'ỵ' };
            return text.Any(c => viChars.Contains(c));
        }

        private static Locale? FindBestLocale(IEnumerable<Locale> locales, string languageCode)
        {
            if (locales == null) return null;
            if (string.IsNullOrWhiteSpace(languageCode)) return locales.FirstOrDefault();

            var normalized = languageCode.Trim();
            var parts = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var lang = parts.Length > 0 ? parts[0].ToLowerInvariant() : normalized.ToLowerInvariant();
            var country = parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty;

            // Prefer exact language+country match when possible
            var exact = locales.FirstOrDefault(l =>
                string.Equals(l.Language, lang, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(country) || string.Equals(l.Country, country, StringComparison.OrdinalIgnoreCase)));
            if (exact != null) return exact;

            // Fallback: language only
            return locales.FirstOrDefault(l => string.Equals(l.Language, lang, StringComparison.OrdinalIgnoreCase));
        }

        private static async Task<bool> PlayFileAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return false;
            }

#if ANDROID
            return await TouristGuideApp.Platforms.Android.AudioPlayback.PlayAsync(filePath);
#elif IOS
            return await TouristGuideApp.Platforms.iOS.AudioPlayback.PlayAsync(filePath);
#else
            return false;
#endif
        }

        private static async Task<bool> TrySynthesizeOfflineToFileAsync(string text, string languageCode, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

#if ANDROID
            try
            {
                var rate = (float)AppPreferences.GetNarrationSpeechRate();
                return await TouristGuideApp.Platforms.Android.OfflineTtsToFile.SynthesizeToWavAsync(text, languageCode, outputPath, rate);
            }
            catch
            {
                return false;
            }
#else
            await Task.CompletedTask;
            return false;
#endif
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
