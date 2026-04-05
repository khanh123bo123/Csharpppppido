using System;
using System.Threading.Tasks;
using Microsoft.Maui.Devices.Sensors;

namespace TouristGuideApp.Services
{
    public interface ILocationService
    {
        event EventHandler<Microsoft.Maui.Devices.Sensors.Location> LocationUpdated;
        void StartTracking();
        void StopTracking();
    }

    public class LocationService : ILocationService
    {
        public event EventHandler<Microsoft.Maui.Devices.Sensors.Location> LocationUpdated;
        private bool _isTracking;

        public async void StartTracking()
        {
            if (_isTracking) return;
            _isTracking = true;

            while (_isTracking)
            {
                try
                {
                    // Lấy vị trí với độ chính xác trung bình (Medium) để tiết kiệm pin
                    var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5));
                    var location = await Geolocation.Default.GetLocationAsync(request);

                    if (location != null)
                    {
                        LocationUpdated?.Invoke(this, location);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Lỗi GPS: {ex.Message}");
                }

                // Cập nhật sau mỗi 5 giây
                await Task.Delay(5000);
            }
        }

        public void StopTracking() => _isTracking = false;
    }
}
