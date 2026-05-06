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
        private readonly IAuthService _authService;
        private bool _autoPlayAfterLoad;
        private bool _openedFromQr;

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
                    _openedFromQr = true;
                    _autoPlayAfterLoad = true;
                    _ = LoadPoiFromQrAsync(Uri.UnescapeDataString(_qr));
                }
            }
        }

        public POIDetailsPage(IGeofenceService geofenceService, IApiService apiService, IDatabaseService databaseService, IAudioService audioService, IAuthService authService)
        {
            InitializeComponent();
            _geofenceService = geofenceService;
            _apiService = apiService;
            _databaseService = databaseService;
            _audioService = audioService;
            _authService = authService;
            
            this.BindingContext = this;
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
                var currentLanguage = AppPreferences.GetNarrationLanguageCode();
                
                if (location != null)
                {
                    var poi = MapLocationToPoi(location, currentLanguage);
                    
                    // Fetch translation for this specific location
                    if (!string.Equals(currentLanguage, SupportedLanguages.Vietnamese, StringComparison.OrdinalIgnoreCase))
                    {
                        var localization = await _apiService.GetLocalizationAsync(serverLocationId, currentLanguage);
                        if (localization != null)
                        {
                            // If the API failed to translate, it often returns the original Vietnamese text.
                            // We check if the 'localized' text is actually just the same as the original.
                            bool isRealTranslation = !string.Equals(localization.LocalizedName?.Trim(), poi.Name?.Trim(), StringComparison.OrdinalIgnoreCase);
                            
                            if (isRealTranslation)
                            {
                                if (!string.IsNullOrWhiteSpace(localization.LocalizedName))
                                    poi.Name = localization.LocalizedName.Trim();
                                
                                if (!string.IsNullOrWhiteSpace(localization.LocalizedDescription))
                                {
                                    poi.Description = localization.LocalizedDescription.Trim();
                                    poi.LanguageCode = currentLanguage;
                                }
                            }
                            else 
                            {
                                // If it's not a real translation, we keep Vietnamese but we MUST 
                                // tell the AudioService that the content is still vi-VN to avoid foreign accent.
                                poi.LanguageCode = "vi-VN";
                            }
                        }
                    }

                    await _databaseService.SavePOIAsync(poi);
                    await SetPoiAndMaybeAutoPlayAsync(poi);
                    return;
                }

                await DisplayAlertAsync("Không tìm thấy", "Không tìm thấy địa điểm cho mã này.", "OK");
            }
            catch
            {
                await DisplayAlertAsync("Offline", "Không có dữ liệu địa điểm trong máy và cũng không thể tải từ Internet.", "OK");
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            // If we have a POI loaded, but the global language changed while we were away, reload it
            var expectedLang = AppPreferences.GetNarrationLanguageCode();
            if (_poi != null && !string.Equals(_poi.LanguageCode, expectedLang, StringComparison.OrdinalIgnoreCase))
            {
                // Force reload
                await LoadPoiFromServerLocationIdAsync(_poi.ServerLocationId.ToString());
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

                await DisplayAlertAsync("Offline", "Điểm này chưa được lưu offline. Hãy mở app khi có wifi để tải dữ liệu trước.", "OK");
                return;
            }

            var qrCodeData = ExtractQrCodeData(rawQr);
            if (string.IsNullOrWhiteSpace(qrCodeData))
            {
                await DisplayAlertAsync("Lỗi QR", "Mã QR không hợp lệ.", "OK");
                return;
            }

            var localPoi = await _databaseService.GetPoiByQrCodeDataAsync(qrCodeData);
            if (localPoi != null)
            {
                await SetPoiAndMaybeAutoPlayAsync(localPoi);
                return;
            }

            await DisplayAlertAsync("Offline", "Mã QR hợp lệ nhưng địa điểm chưa được lưu offline. Hãy mở app khi có wifi để tải dữ liệu trước.", "OK");
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

        private static POI MapLocationToPoi(TouristGuideApp.Models.Location location, string targetLanguage)
        {
            return new POI
            {
                ServerLocationId = location.Id,
                QrCodeData = string.IsNullOrWhiteSpace(location.QrCodeData) ? null : location.QrCodeData.Trim(),
                Name = location.Name ?? "Chưa đặt tên",
                Description = location.Description ?? "Không có mô tả",
                Category = string.IsNullOrWhiteSpace(location.Category) ? "Chưa phân loại" : location.Category,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                Address = location.Address,
                PhoneNumber = location.PhoneNumber,
                ImageUrl = location.ImageUrl,
                AudioUrl = location.AudioUrl,
                LanguageCode = targetLanguage
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
                await _geofenceService.PlaySpeechAsync(poi, ignoreCooldown: true, forceOfflineTts: _openedFromQr);
            }
        }

        private void LoadPOIDetails()
        {
            if (_poi == null) return;

            _poi.ImageUrl = HtmlUtils.EnsureAbsoluteUrl(_poi.ImageUrl, _apiService.BaseAddress);
            var processedHtml = HtmlUtils.FixImageUrls(_poi.Description, _apiService.BaseAddress);
            var finalHtml = HtmlUtils.WrapInMobileLayout(processedHtml);
            
            wvDescription.Source = new HtmlWebViewSource { Html = finalHtml };

            if (!string.IsNullOrWhiteSpace(_poi.ImageUrl))
            {
                // Ensure absolute URL
                if (!_poi.ImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    var baseUrl = _apiService.BaseAddress.TrimEnd('/');
                    _poi.ImageUrl = $"{baseUrl}/{_poi.ImageUrl.TrimStart('/')}";
                }
                
                imgLocation.Source = ImageSource.FromUri(new Uri(_poi.ImageUrl));
                OnPropertyChanged(nameof(POIItem));
            }
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private void OnDescriptionWebViewNavigated(object sender, WebNavigatedEventArgs e)
        {
            // Optional: dynamically adjust WebView height if needed
        }

        private async void OnCallTapped(object sender, EventArgs e)
        {
            if (_poi != null && !string.IsNullOrWhiteSpace(_poi.PhoneNumber))
            {
                if (PhoneDialer.Default.IsSupported)
                    PhoneDialer.Default.Open(_poi.PhoneNumber);
            }
            else
            {
                await DisplayAlertAsync("Thông báo", "Địa điểm này chưa cập nhật số điện thoại.", "OK");
            }
        }

        private async void OnDirectionTapped(object sender, EventArgs e)
        {
            if (_poi != null)
            {
                var location = new Microsoft.Maui.Devices.Sensors.Location(_poi.Latitude, _poi.Longitude);
                var options = new MapLaunchOptions { Name = _poi.Name, NavigationMode = NavigationMode.Driving };

                try
                {
                    await Microsoft.Maui.ApplicationModel.Map.Default.OpenAsync(location, options);
                }
                catch (Exception)
                {
                    await DisplayAlertAsync("Lỗi", "Không thể mở ứng dụng bản đồ.", "OK");
                }
            }
        }

        private async void OnListenClicked(object sender, EventArgs e)
        {
            if (_poi == null) return;
            
            // Stop any existing playback first to ensure we can play again immediately
            await _audioService.StopAsync();
            
            var currentLanguage = AppPreferences.GetNarrationLanguageCode();
            string? audioUrlToUse = _poi.AudioUrl;
            
            // If we are in a non-Vietnamese mode, the default 'AudioUrl' (usually Vietnamese)
            // should NOT be played. This forces AudioService to look for a localized MP3 or use TTS.
            if (!string.Equals(currentLanguage, SupportedLanguages.Vietnamese, StringComparison.OrdinalIgnoreCase))
            {
                audioUrlToUse = null;
            }

            await _audioService.EnqueueSpeechAsync(_poi.Description, _poi.ServerLocationId, audioUrlToUse);
        }

        private async void OnStarClicked(object sender, EventArgs e)
        {
            if (_poi == null || sender is not Button btn) return;

            if (int.TryParse(btn.CommandParameter?.ToString(), out int stars))
            {
                lblRatingStatus.Text = "Đang gửi đánh giá...";
                
                string deviceId = DeviceInfo.Current.Name;
                string? userEmail = _authService.UserEmail;

                bool success = await _apiService.SubmitRatingAsync(_poi.ServerLocationId, stars, deviceId, userEmail);
                
                if (success)
                {
                    lblRatingStatus.Text = $"Cảm ơn bạn đã đánh giá {stars} sao!";
                    lblRatingStatus.TextColor = Colors.Green;
                }
                else
                {
                    lblRatingStatus.Text = "Không thể gửi đánh giá. Vui lòng thử lại!";
                    lblRatingStatus.TextColor = Colors.Red;
                }
            }
        }
    }
}
