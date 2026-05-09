using System;
using Microsoft.Maui.Controls;
using System.Text.RegularExpressions;
using TouristGuideApp.Models;
using TouristGuideApp.Services;

namespace TouristGuideApp.Views
{
    [QueryProperty(nameof(POIItem), "POI")]
    [QueryProperty(nameof(PoiId), "poiId")]
    [QueryProperty(nameof(Qr), "qr")]
    public partial class POIDetailsPage : ContentPage
    {
        private POI _poi = null!;
        private readonly IGeofenceService _geofenceService;
        private readonly IApiService _apiService;
        private readonly IDatabaseService _databaseService;
        private readonly IAudioService _audioService;
        private bool _autoPlayAfterLoad;

        public POI POIItem
        {
            get => _poi;
            set
            {
                _poi = value;
                OnPropertyChanged();
                LoadPOIDetails();
            }
        }

        private string _poiId = string.Empty;
        public string PoiId
        {
            get => _poiId;
            set
            {
                _poiId = value;
                OnPropertyChanged();
                if (!string.IsNullOrWhiteSpace(_poiId))
                {
                    _autoPlayAfterLoad = true;
                    _ = LoadPoiFromServerLocationIdAsync(_poiId);
                }
            }
        }

        private string _qr = string.Empty;
        public string Qr
        {
            get => _qr;
            set
            {
                _qr = value;
                OnPropertyChanged();
                if (!string.IsNullOrWhiteSpace(_qr))
                {
                    _autoPlayAfterLoad = true;
                    _ = LoadPoiFromQrAsync(Uri.UnescapeDataString(_qr));
                }
            }
        }

        public POIDetailsPage(IGeofenceService geofenceService, IApiService apiService, IDatabaseService databaseService, IAudioService audioService)
        {
            InitializeComponent();
            _geofenceService = geofenceService;
            _apiService = apiService;
            _databaseService = databaseService;
            _audioService = audioService;
        }

        private async Task LoadPoiFromServerLocationIdAsync(string idString)
        {
            if (!int.TryParse(idString, out int serverLocationId) || serverLocationId <= 0)
            {
                return;
            }

            // OFFLINE-FIRST: load cached POI from SQLite
            var localPoi = await _databaseService.GetPoiByServerLocationIdAsync(serverLocationId);
            if (localPoi != null)
            {
                await SetPoiAndMaybeAutoPlayAsync(localPoi);
                return;
            }

            // Online fallback
            try
            {
                var location = await _apiService.GetLocationAsync(serverLocationId);
                if (location != null)
                {
                    var poi = MapLocationToPoi(location);
                    await _databaseService.SavePOIAsync(poi);
                    await SetPoiAndMaybeAutoPlayAsync(poi);
                    return;
                }

                await DisplayAlert("Không tìm thấy", "Không tìm thấy địa điểm cho mã này.", "OK");
            }
            catch
            {
                await DisplayAlert("Offline", "Không có dữ liệu địa điểm trong máy và cũng không thể tải từ Internet.", "OK");
            }
        }

        private async Task LoadPoiFromQrAsync(string rawQr)
        {
            if (string.IsNullOrWhiteSpace(rawQr))
            {
                return;
            }

            // QR requirement: OFFLINE-ONLY. Do not call API when scanning.
            // Support both legacy numeric ids and real QR payloads (LOC_*).
            var serverLocationId = TryExtractServerLocationId(rawQr);
            if (serverLocationId is > 0)
            {
                var localById = await _databaseService.GetPoiByServerLocationIdAsync(serverLocationId.Value);
                if (localById != null)
                {
                    await SetPoiAndMaybeAutoPlayAsync(localById);
                    return;
                }

                await DisplayAlert("Offline", "Điểm này chưa được lưu offline. Hãy mở app khi có wifi để tải dữ liệu trước.", "OK");
                return;
            }

            var qrCodeData = ExtractQrCodeData(rawQr);
            if (string.IsNullOrWhiteSpace(qrCodeData))
            {
                await DisplayAlert("Lỗi QR", "Mã QR không hợp lệ.", "OK");
                return;
            }

            var localPoi = await _databaseService.GetPoiByQrCodeDataAsync(qrCodeData);
            if (localPoi != null)
            {
                await SetPoiAndMaybeAutoPlayAsync(localPoi);
                return;
            }

            await DisplayAlert("Offline", "Mã QR hợp lệ nhưng địa điểm chưa được lưu offline. Hãy mở app khi có wifi để tải dữ liệu trước.", "OK");
        }

        private static int? TryExtractServerLocationId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var trimmed = value.Trim();
            if (trimmed.StartsWith("poi:", StringComparison.OrdinalIgnoreCase))
            {
                var rest = trimmed.Substring(4);
                if (int.TryParse(rest, out var id)) return id;
            }

            if (Regex.IsMatch(trimmed, @"^\d+$") && int.TryParse(trimmed, out var direct))
            {
                return direct;
            }

            var match = Regex.Match(trimmed, @"\b(\d+)\b");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var extracted))
            {
                return extracted;
            }

            return null;
        }

        private static string? ExtractQrCodeData(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var trimmed = raw.Trim();

            // Most common payload: LOC_{Guid}
            if (trimmed.StartsWith("LOC_", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            // If QR contains a URL/text, try to locate LOC_... inside
            var match = Regex.Match(trimmed, @"LOC_[A-Za-z0-9]+", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Value;
            }

            // Fallback: treat raw as code
            return trimmed;
        }

        private static POI MapLocationToPoi(TouristGuideApp.Models.Location location)
        {
            return new POI
            {
                ServerLocationId = location.Id,
                QrCodeData = string.IsNullOrWhiteSpace(location.QrCodeData) ? null : location.QrCodeData.Trim(),
                Name = location.Name ?? "Chưa đặt tên",
                Description = location.Description ?? "Không có mô tả",
                Category = location.Category ?? string.Empty,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                Address = location.Address,
                PhoneNumber = location.PhoneNumber,
                ImageUrl = location.ImageUrl,
                AudioUrl = location.AudioUrl,
                LanguageCode = "vi-VN"
            };
        }

        private async Task SetPoiAndMaybeAutoPlayAsync(POI poi)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                POIItem = poi;
            });

            if (_autoPlayAfterLoad)
            {
                _autoPlayAfterLoad = false;
                await _geofenceService.PlaySpeechAsync(poi, ignoreCooldown: true);
            }
        }

        public override string? ToString() => _poi?.Name ?? base.ToString();

        private void LoadPOIDetails()
        {
            if (_poi == null) return;

            lblName.Text = _poi.Name;
            lblDistance.Text = $"Cách bạn {_poi.DistanceText}";
            lblDescription.Text = _poi.Description;

            lblPhone.Text = string.IsNullOrWhiteSpace(_poi.PhoneNumber) ? "Chưa cập nhật SĐT" : _poi.PhoneNumber;

            if (!string.IsNullOrWhiteSpace(_poi.ImageUrl))
            {
                imgLocation.IsVisible = true;
                imgLocation.Source = ImageSource.FromUri(new Uri(_poi.ImageUrl));
            }
            else
            {
                imgLocation.IsVisible = false;
            }
        }

        private async void OnListenClicked(object sender, EventArgs e)
        {
            if (_poi != null)
            {
                await _geofenceService.PlaySpeechAsync(_poi, ignoreCooldown: true);
            }
        }

        private async void OnDownloadAudioClicked(object sender, EventArgs e)
        {
            if (_poi == null || _poi.ServerLocationId <= 0)
            {
                await DisplayAlert("Không thể lưu", "Địa điểm này chưa có dữ liệu audio từ server.", "OK");
                return;
            }

            btnDownloadAudio.IsEnabled = false;
            btnDownloadAudio.Text = "Đang tải...";

            try
            {
                var saved = await _audioService.SaveLocationAudioAsync(_poi.ServerLocationId, _poi.AudioUrl);
                if (saved)
                {
                    await DisplayAlert("Đã lưu", "Đã tải và lưu file MP3 offline vào máy.", "OK");
                }
                else
                {
                    await DisplayAlert("Thất bại", "Chưa tải được MP3. Hãy thử lại khi có mạng.", "OK");
                }
            }
            finally
            {
                btnDownloadAudio.Text = "Lưu MP3 offline";
                btnDownloadAudio.IsEnabled = true;
            }
        }
    }
}
