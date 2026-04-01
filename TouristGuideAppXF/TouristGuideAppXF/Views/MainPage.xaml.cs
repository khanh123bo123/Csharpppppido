using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;
using TouristGuideAppXF.Services;
using ZXing.Net.Mobile.Forms;
using TouristGuideAppXF.Models;

namespace TouristGuideAppXF.Views
{
    public partial class MainPage
    {
        private Models.Location currentLocation;

        public MainPage()
        {
            InitializeComponent();
        }

        private async void OnScanClicked(object sender, EventArgs e)
        {
            var hasCameraPermission = await RequestCameraPermissionAsync();
            var hasLocationPermission = await RequestLocationPermissionAsync();

            if (!hasCameraPermission)
            {
                await DisplayAlert("Lỗi", "Cần cấp quyền camera để quét QR", "OK");
                return;
            }

            try
            {
                var scanPage = new ZXingScannerPage();
                scanPage.OnScanResult += (result) =>
                {
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        scanPage.IsScanning = false;
                        await Navigation.PopAsync();
                        await ProcessScannedQr(result.Text);
                    });
                };

                await Navigation.PushAsync(scanPage);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Lỗi quét QR: {ex.Message}", "OK");
            }
        }

        private async Task ProcessScannedQr(string qrCode)
        {
            LoadingIndicator.IsRunning = true;
            LoadingIndicator.IsVisible = true;
            LocationInfoLayout.IsVisible = false;
            WarningLabel.IsVisible = false;

            try
            {
                var location = await App.ApiService.GetLocationByQrCode(qrCode);
                if (location == null)
                {
                    await DisplayAlert("Thông báo", "Không tìm thấy điểm tham quan", "OK");
                    LoadingIndicator.IsRunning = false;
                    LoadingIndicator.IsVisible = false;
                    return;
                }

                // Lấy khoảng cách và xử lý exception
                double distance = await GetDistanceToLocation(location);

                currentLocation = location;

                // Cập nhật UI
                NameLabel.Text = location.Name;
                DescriptionLabel.Text = location.Description;
                DistanceLabel.Text = $"Cách bạn: {Math.Round(distance)} mét";

                // Hiển thị thông báo đề xuất chi tiết dựa trên khoảng cách
                if (distance < 0)
                {
                    WarningLabel.Text = "❌ Không thể xác định vị trí của bạn. Vui lòng bật GPS và thử lại.";
                    WarningLabel.TextColor = Color.FromHex("#FF3B30");
                    WarningLabel.IsVisible = true;
                }
                else if (distance <= 200)
                {
                    WarningLabel.Text = "✅ Bạn đang ở gần điểm tham quan này. Thuyết minh phù hợp!";
                    WarningLabel.TextColor = Color.FromHex("#34C759");
                    WarningLabel.IsVisible = true;
                }
                else
                {
                    WarningLabel.Text = $"⚠️ Bạn đang ở xa điểm này (khoảng {distance:F0} mét). Nội dung thuyết minh có thể không phù hợp.";
                    WarningLabel.TextColor = Color.FromHex("#FF9500");
                    WarningLabel.IsVisible = true;
                }

                LocationInfoLayout.IsVisible = true;
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Lỗi xử lý QR: {ex.Message}", "OK");
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
            }
        }

        private async void OnPlayAudioClicked(object sender, EventArgs e)
        {
            if (currentLocation == null || string.IsNullOrEmpty(currentLocation.AudioUrl))
            {
                await DisplayAlert("Lỗi", "Không có file thuyết minh cho điểm này", "OK");
                return;
            }

            try
            {
                AudioIndicator.IsRunning = true;
                AudioIndicator.IsVisible = true;
                PlayAudioButton.IsEnabled = false;

                // Download file âm thanh
                using (var client = new HttpClient())
                {
                    var audioBytes = await client.GetByteArrayAsync(currentLocation.AudioUrl);
                    var audioPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "audio.mp3");

                    File.WriteAllBytes(audioPath, audioBytes);

                    // Phát âm thanh bằng MediaElement (nếu có) hoặc platform native
                    // Tạm thời dùng Xamarin.Essentials MediaElement (nếu hỗ trợ)
                    // Hoặc dùng WebView
                    await PlayAudioFile(audioPath);
                }

                AudioIndicator.IsRunning = false;
                AudioIndicator.IsVisible = false;
                PlayAudioButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi phát âm thanh", ex.Message, "OK");
                AudioIndicator.IsRunning = false;
                AudioIndicator.IsVisible = false;
                PlayAudioButton.IsEnabled = true;
            }
        }

        private async Task PlayAudioFile(string audioPath)
        {
            // Tạm thời: hiển thị thông báo rằng file đã tải xuống
            // Khi chạy trên Android, platform-specific code sẽ xử lý phát audio
            await DisplayAlert("Âm thanh", "File thuyết minh đã tải xuống. Nhấn OK để phát.", "OK");
        }

        private async Task<double> GetDistanceToLocation(Models.Location location)
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(30));
                var currentPosition = await Geolocation.GetLocationAsync(request);
                if (currentPosition == null)
                    return -1;

                return CalculateDistance(
                    currentPosition.Latitude, currentPosition.Longitude,
                    location.Latitude, location.Longitude);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting location: {ex.Message}");
                return -1;
            }
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000; // Bán kính Trái Đất (mét)
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private async Task<bool> RequestCameraPermissionAsync()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.Camera>();
                }
                return status == PermissionStatus.Granted;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> RequestLocationPermissionAsync()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                }
                return status == PermissionStatus.Granted;
            }
            catch
            {
                return false;
            }
        }
    }
}

