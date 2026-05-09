using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Networking;

namespace TouristGuideApp.Services
{
    /// <summary>
    /// API-first audio system:
    /// - Play cached API audio when available.
    /// - Download and cache API audio when online.
    /// - No offline/on-device TTS synthesis fallback.
    /// </summary>
    public interface IAudioService
    {
        Task EnqueueSpeechAsync(string text, int? serverLocationId = null, string? cachedAudioUrl = null, Action? onStarted = null, Action? onEnded = null);
        bool IsPlaying { get; }
        Task SetLanguageAsync(string languageCode);
        string CurrentLanguage { get; }
        Task PrefetchAudioAsync(string poiName, string languageCode);
        Task ClearCacheAsync();
        string GetCacheFolderPath();
        IReadOnlyList<string> GetCachedAudioFiles();
        Task<bool> SaveLocationAudioAsync(int serverLocationId, string? audioUrl = null);
        Task<int> DeleteCachedAudioFilesAsync(IEnumerable<string> fileNames);
    }

    public class AudioService : IAudioService
    {
        private readonly IApiService _apiService;
        private readonly Queue<(string Text, int? ServerLocationId, string? AudioUrl, Action? OnStarted, Action? OnEnded)> _speechQueue = new();
        private bool _isProcessing = false;
        public bool IsPlaying { get; private set; }
        public string CurrentLanguage { get; private set; } = "vi-VN";

        public AudioService(IApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task SetLanguageAsync(string languageCode)
        {
            CurrentLanguage = languageCode;
            await Task.CompletedTask;
        }

        public async Task EnqueueSpeechAsync(string text, int? serverLocationId = null, string? cachedAudioUrl = null, Action? onStarted = null, Action? onEnded = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            _speechQueue.Enqueue((text, serverLocationId, cachedAudioUrl, onStarted, onEnded));

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

                    if (item.ServerLocationId is > 0)
                    {
                        playedAudio = await TryPlayFromCacheOrApiAsync(item.ServerLocationId.Value);
                    }

                    if (!playedAudio && !string.IsNullOrWhiteSpace(item.AudioUrl))
                    {
                        playedAudio = await TryPlayLocalCachedAudioAsync(item.AudioUrl);
                    }

                    if (!playedAudio)
                    {
                        System.Diagnostics.Debug.WriteLine("AudioService: API audio unavailable, skipping playback.");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Audio Error: {ex.Message}");
                }

                IsPlaying = false;
                item.OnEnded?.Invoke();
                await Task.Delay(300);
            }

            _isProcessing = false;
        }

        private async Task<bool> TryPlayFromCacheOrApiAsync(int serverLocationId)
        {
            try
            {
                EnsureCacheFolderExists();

                // Get localization first to get the ID for recording play count
                var localization = default(Models.LocalizationModel);
                if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                {
                    localization = await _apiService.GetLocalizationAsync(serverLocationId, CurrentLanguage);
                }

                var existing = FindExistingCachedFile(serverLocationId, CurrentLanguage);
                if (!string.IsNullOrWhiteSpace(existing))
                {
                    var played = await PlayFileAsync(existing);
                    if (played && localization != null && localization.Id > 0)
                    {
                        _ = _apiService.RecordPlayAsync(localization.Id); // Fire and forget
                    }
                    return played;
                }

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
                            var played = await PlayFileAsync(mp3Path);
                            if (played)
                            {
                                _ = _apiService.RecordPlayAsync(localization.Id); // Fire and forget
                            }
                            return played;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to play/cache API audio: {ex.Message}");
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
            return null;
        }

        private static string GetOnlineMp3CachePath(int serverLocationId, string languageCode)
        {
            var safeLang = (languageCode ?? "vi-VN").Replace('/', '_').Replace('\\', '_');
            return Path.Combine(FileSystem.AppDataDirectory, "tts_cache", $"poi_{serverLocationId}_{safeLang}.mp3");
        }

        private static async Task<bool> TryPlayLocalCachedAudioAsync(string audioUrl)
        {
            try
            {
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

        public async Task PrefetchAudioAsync(string poiName, string languageCode)
        {
            await Task.CompletedTask;
        }

        public string GetCacheFolderPath() => Path.Combine(FileSystem.AppDataDirectory, "tts_cache");

        public IReadOnlyList<string> GetCachedAudioFiles()
        {
            var dir = GetCacheFolderPath();
            if (!Directory.Exists(dir)) return Array.Empty<string>();
            return Directory.GetFiles(dir, "*.mp3").OrderBy(Path.GetFileName).ToList();
        }

        public async Task<bool> SaveLocationAudioAsync(int serverLocationId, string? audioUrl = null)
        {
            try
            {
                EnsureCacheFolderExists();

                if (serverLocationId <= 0)
                {
                    return false;
                }

                var localization = await _apiService.GetLocalizationAsync(serverLocationId, CurrentLanguage);
                if (localization == null || localization.Id <= 0)
                {
                    return false;
                }

                if (!string.Equals(localization.AudioGenerationStatus, "generated", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(localization.AudioGenerationStatus, "cached", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var audioBytes = await _apiService.GetLocalizationAudioBytesAsync(localization.Id);
                if (audioBytes == null || audioBytes.Length == 0)
                {
                    return false;
                }

                var mp3Path = GetOnlineMp3CachePath(serverLocationId, CurrentLanguage);
                await File.WriteAllBytesAsync(mp3Path, audioBytes);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save location audio: {ex.Message}");
                return false;
            }
        }

        public async Task<int> DeleteCachedAudioFilesAsync(IEnumerable<string> fileNames)
        {
            var deleted = 0;
            var dir = GetCacheFolderPath();
            if (!Directory.Exists(dir)) return 0;

            foreach (var fileName in fileNames.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                var path = Path.IsPathRooted(fileName) ? fileName : Path.Combine(dir, fileName);
                if (!File.Exists(path)) continue;

                try
                {
                    File.Delete(path);
                    deleted++;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to delete cached audio {path}: {ex.Message}");
                }
            }

            await Task.CompletedTask;
            return deleted;
        }

        public Task ClearCacheAsync()
        {
            var dir = GetCacheFolderPath();
            if (Directory.Exists(dir))
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to clear cache: {ex.Message}");
                }
            }
            return Task.CompletedTask;
        }
    }
}
