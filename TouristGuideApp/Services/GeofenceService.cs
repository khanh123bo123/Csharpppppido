using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TouristGuideApp.Models;
using Microsoft.Maui.Devices.Sensors;

namespace TouristGuideApp.Services
{
    public interface IGeofenceService
    {
        Task InitAsync();
        Task CheckProximity(Microsoft.Maui.Devices.Sensors.Location userLocation);
        Task PlaySpeechAsync(POI poi, bool ignoreCooldown = false);
        List<POI> GetPOIs();
        POI? ActivePOI { get; }
    }

    public class GeofenceService : IGeofenceService
    {
        private readonly IAudioService _audioService;
        private readonly IDatabaseService _databaseService;
        private List<POI> _pois = new();
        public POI? ActivePOI { get; private set; }

        public GeofenceService(IAudioService audioService, IDatabaseService databaseService)
        {
            _audioService = audioService;
            _databaseService = databaseService;
        }

        public async Task InitAsync()
        {
            _pois = await _databaseService.GetPOIsAsync();
        }

        public List<POI> GetPOIs() => _pois;

        public async Task PlaySpeechAsync(POI poi, bool ignoreCooldown = false)
        {
            double secondsSinceLastPlay = (DateTime.Now - poi.LastPlayedTime).TotalSeconds;

            // Nếu là bấm tay (ignoreCooldown = true) HOẶC đã quá 5 phút (300s)
            if (ignoreCooldown || secondsSinceLastPlay > 300)
            {
                poi.LastPlayedTime = DateTime.Now;

                await _audioService.EnqueueSpeechAsync(
                    poi.Description,
                    () => { poi.IsCurrentlyPlaying = true; },
                    () => {
                        poi.IsCurrentlyPlaying = false;
                        poi.HasBeenPlayed = true;
                    }
                );
            }
        }

        public async Task CheckProximity(Microsoft.Maui.Devices.Sensors.Location userLocation)
        {
            foreach (var poi in _pois)
            {
                double distanceInMeters = Microsoft.Maui.Devices.Sensors.Location.CalculateDistance(
                    userLocation,
                    poi.Latitude,
                    poi.Longitude,
                    DistanceUnits.Kilometers) * 1000;

                if (distanceInMeters <= poi.Radius)
                {
                    ActivePOI = poi;
                    // Tự động phát khi vào vùng (có áp dụng cooldown)
                    await PlaySpeechAsync(poi, ignoreCooldown: false);
                    return;
                }
            }
            ActivePOI = null;
        }
    }
}
