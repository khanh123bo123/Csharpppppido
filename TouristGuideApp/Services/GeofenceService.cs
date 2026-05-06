using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TouristGuideApp.Models;
using Microsoft.Maui.Devices.Sensors;

namespace TouristGuideApp.Services
{
    /// <summary>
    /// Geofencing service with 30m proximity detection and multi-language audio narration
    /// </summary>
    public interface IGeofenceService
    {
        Task InitAsync();
        Task CheckProximity(Microsoft.Maui.Devices.Sensors.Location userLocation);
        Task PlaySpeechAsync(POI poi, bool ignoreCooldown = false, bool forceOfflineTts = false);
        List<POI> GetPOIs();
        POI? ActivePOI { get; }
        Task SetLanguageAsync(string languageCode);
        string CurrentLanguage { get; }
    }

    public class GeofenceService : IGeofenceService
    {
        private readonly IAudioService _audioService;
        private readonly IDatabaseService _databaseService;
        private readonly IApiService _apiService;
        private List<POI> _pois = new();
        public POI? ActivePOI { get; private set; }
        public string CurrentLanguage { get; private set; } = "vi-VN";

        // Cooldown period between audio plays (in seconds)
        private const double AudioCooldownSeconds = 300; // 5 minutes

        public GeofenceService(IAudioService audioService, IDatabaseService databaseService, IApiService apiService)
        {
            _audioService = audioService;
            _databaseService = databaseService;
            _apiService = apiService;
        }

        public async Task SetLanguageAsync(string languageCode)
        {
            CurrentLanguage = languageCode;
            await _audioService.SetLanguageAsync(languageCode);
            System.Diagnostics.Debug.WriteLine($"GeofenceService language changed to {languageCode}");
        }

        public async Task InitAsync()
        {
            // Load all POIs from database
            _pois = await _databaseService.GetPOIsAsync();
            System.Diagnostics.Debug.WriteLine($"GeofenceService initialized with {_pois.Count} POIs");
        }

        public List<POI> GetPOIs() => _pois;

        /// <summary>
        /// Play audio narration for a POI with 4-tier fallback system
        /// </summary>
        public async Task PlaySpeechAsync(POI poi, bool ignoreCooldown = false, bool forceOfflineTts = false)
        {
            double secondsSinceLastPlay = (DateTime.Now - poi.LastPlayedTime).TotalSeconds;

            if (ignoreCooldown || secondsSinceLastPlay > AudioCooldownSeconds)
            {
                poi.LastPlayedTime = DateTime.Now;
                
                // Tier 1: Check if POI description is already in the target language
                var textToPlay = poi.Description;
                
                if (!string.Equals(poi.LanguageCode, CurrentLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    // Tier 2: Try to fetch localization from API if online
                    try 
                    {
                        var loc = await _apiService.GetLocalizationAsync(poi.ServerLocationId, CurrentLanguage);
                        if (loc != null && !string.IsNullOrWhiteSpace(loc.LocalizedDescription))
                        {
                            // If the localization is identical to the name (indicating a failed AI translate), ignore it
                            bool isRealTranslation = !string.Equals(loc.LocalizedName?.Trim(), poi.Name?.Trim(), StringComparison.OrdinalIgnoreCase);
                            
                            if (isRealTranslation)
                            {
                                textToPlay = loc.LocalizedDescription;
                                // Optionally update local cache so next time it's offline-ready
                                poi.Description = textToPlay;
                                poi.LanguageCode = CurrentLanguage;
                                await _databaseService.SavePOIAsync(poi);
                            }
                            else 
                            {
                                System.Diagnostics.Debug.WriteLine($"[GeofenceService] Localization for {poi.Name} in {CurrentLanguage} is same as original. Staying in vi-VN mode.");
                                poi.LanguageCode = "vi-VN";
                            }
                        }
                    }
                    catch { /* Fallback to default if offline / API error */ }
                }

                // Final safety: if text is still empty, use POI name
                if (string.IsNullOrWhiteSpace(textToPlay))
                {
                    textToPlay = poi.Name ?? "Bạn đang ở gần một địa điểm du lịch.";
                }

                await _audioService.EnqueueSpeechAsync(
                    textToPlay,
                    serverLocationId: poi.ServerLocationId > 0 ? poi.ServerLocationId : null,
                    forceOfflineTts: forceOfflineTts,
                    onStarted: () => { poi.IsCurrentlyPlaying = true; },
                    onEnded: () => {
                        poi.IsCurrentlyPlaying = false;
                        poi.HasBeenPlayed = true;
                    }
                );
            }
        }

        /// <summary>
        /// Check proximity to all POIs and trigger audio if within radius
        /// Uses Haversine formula for accurate distance calculation
        /// </summary>
        public async Task CheckProximity(Microsoft.Maui.Devices.Sensors.Location userLocation)
        {
            if (userLocation == null) return;

            // 1. Calculate distance to all POIs
            foreach (var poi in _pois)
            {
                poi.DistanceToUser = Microsoft.Maui.Devices.Sensors.Location.CalculateDistance(
                    userLocation,
                    poi.Latitude,
                    poi.Longitude,
                    DistanceUnits.Kilometers) * 1000; // Convert to meters
            }

            // 2. Sort by distance (nearest first)
            _pois = _pois.OrderBy(p => p.DistanceToUser).ToList();

            // 3. Find nearest POI within activation radius (30m default)
            ActivePOI = _pois.FirstOrDefault(p => p.DistanceToUser <= p.Radius);

            if (ActivePOI != null)
            {
                System.Diagnostics.Debug.WriteLine($"Nearby POI detected: {ActivePOI.Name} ({ActivePOI.DistanceToUser:F1}m away)");
                await PlaySpeechAsync(ActivePOI, ignoreCooldown: false);
            }
        }
    }
}
