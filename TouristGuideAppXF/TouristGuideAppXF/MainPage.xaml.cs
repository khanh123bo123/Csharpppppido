using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Essentials;
using ZXing.Net.Mobile.Forms;
using TouristGuideAppXF.Models;
using TouristGuideAppXF.Services;

namespace TouristGuideAppXF
{
    public partial class MainPage : ContentPage
    {
        private Models.Location _currentLocation;

        public MainPage()
        {
            InitializeComponent();
        }

        private async void OnScanClicked(object sender, EventArgs e)
        {
            try
            {
                // Kiểm tra quyền Camera và Location
                await RequestPermissionsAsync();

                // Khởi động scan page
                var scanPage = new ZXingScannerPage();
                scanPage.OnScanResult += async (result) =>
                {
                    scanPage.IsScanning = false;
                    await Navigation.PopAsync();
                    await ProcessScannedQr(result.Text);
                };

                await Navigation.PushAsync(scanPage);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Không thể mở camera: {ex.Message}", "OK");
            }
        }

        private async Task RequestPermissionsAsync()
        {
            try
            {
                var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (cameraStatus != PermissionStatus.Granted)
                {
                    cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
                }

                var locationStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (locationStatus != PermissionStatus.Granted)
                {
                    locationStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                }

                if (cameraStatus != PermissionStatus.Granted)
                {
                    await DisplayAlert("Quyền bị từ chối", "Ứng dụng cần quyền camera để quét QR", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Không thể xin quyền: {ex.Message}", "OK");
            }
        }

        private async Task ProcessScannedQr(string qrCode)
        {
            try
            {
                LoadingIndicator.IsRunning = true;
                LoadingIndicator.IsVisible = true;
                LocationInfoLayout.IsVisible = false;

                // Gọi API lấy location theo QR code
                _currentLocation = await App.ApiService.GetLocationByQrCode(qrCode);

                if (_currentLocation == null)
                {
                    await DisplayAlert("Không tìm thấy", "Không tìm thấy điểm tham quan với mã QR này", "OK");
                    LoadingIndicator.IsRunning = false;
                    LoadingIndicator.IsVisible = false;
                    return;
                }

                // Lấy vị trí hiện tại
                var currentLocation = await Geolocation.GetLocationAsync(new GeolocationRequest
                {
                    DesiredAccuracy = GeolocationAccuracy.Best,
                    Timeout = TimeSpan.FromSeconds(30)
                });

                // Tính khoảng cách
                double distance = 0;
                if (currentLocation != null)
                {
                    distance = CalculateDistance(
                        currentLocation.Latitude,
                        currentLocation.Longitude,
                        _currentLocation.Latitude,
                        _currentLocation.Longitude);
                }

                // Cập nhật UI
                LocationNameLabel.Text = _currentLocation.Name;
                LocationDescriptionLabel.Text = _currentLocation.Description;
                DistanceLabel.Text = $"Cách bạn: {Math.Round(distance, 0)} mét";

                // Cảnh báo nếu quá 100m
                if (distance > 100)
                {
                    await DisplayAlert("Cảnh báo", $"Điểm tham quan cách bạn {Math.Round(distance, 0)} mét", "OK");
                }

                // Hiển thị thông tin location
                LocationInfoLayout.IsVisible = true;
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Lỗi khi xử lý QR: {ex.Message}", "OK");
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
            }
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371000; // mét
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private async void OnPlayAudioClicked(object sender, EventArgs e)
        {
            if (_currentLocation == null || string.IsNullOrEmpty(_currentLocation.AudioUrl))
            {
                await DisplayAlert("Thông báo", "Không có file âm thanh cho điểm này", "OK");
                return;
            }

            try
            {
                // TODO: Triển khai phát âm thanh từ AudioUrl
                await DisplayAlert("Thông báo", "Đặc năng phát âm thanh đang được phát triển", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Không thể phát âm thanh: {ex.Message}", "OK");
            }
        }
    }
}
