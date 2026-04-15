using Microsoft.Maui.Controls;
using TouristGuideApp.Models;
using TouristGuideApp.Services;
using Location = Microsoft.Maui.Devices.Sensors.Location;

namespace TouristGuideApp;

public partial class MainPage : ContentPage
{
    private readonly ILocationService _locationService;
    private readonly IGeofenceService _geofenceService;
    private readonly IApiService _apiService;
    private readonly IDatabaseService _databaseService;
    private bool _isTrackingActive = false;

    public MainPage(ILocationService locationService, IGeofenceService geofenceService, IApiService apiService, IDatabaseService databaseService)
    {
        InitializeComponent();
        _locationService = locationService;
        _geofenceService = geofenceService;
        _apiService = apiService;
        _databaseService = databaseService;

        _locationService.LocationUpdated += OnLocationUpdated;
    }

    private async void OnPOITapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is TouristGuideApp.Models.POI selectedPOI)
        {
            // Navigate to the unified POIDetailsPage, passing the selected POI
            var navigationParameter = new Dictionary<string, object>
            {
                { "POI", selectedPOI }
            };

            await Shell.Current.GoToAsync(nameof(Views.POIDetailsPage), navigationParameter);
        }
    }

    private void OnPOISelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is TouristGuideApp.Models.POI selectedPOI)
        {
            ((CollectionView)sender).SelectedItem = null;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            // First launch: choose narration language pack
            var languageCode = await EnsureNarrationLanguageSelectedAsync();
            await _geofenceService.SetLanguageAsync(languageCode);

            // 1. Hiển thị dữ liệu cũ từ SQLite ngay lập tức
            await _geofenceService.InitAsync();
            UpdateUIList();

            // If there are no cached POIs yet, ensure the API is reachable.
            // On physical Android devices, this commonly requires:
            //   adb reverse tcp:5214 tcp:5214
            // and TourGuideApi running on: http://localhost:5214
            if (!_geofenceService.GetPOIs().Any())
            {
                using var pingCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                var canReachApi = await _apiService.PingAsync(pingCts.Token);
                if (!canReachApi)
                {
                    await DisplayAlert(
                        "Không kết nối được máy chủ",
                        "App chưa tải được dữ liệu địa điểm (POIs).\n\n" +
                        "Nếu chạy trên điện thoại thật qua USB:\n" +
                        "1) Chạy TourGuideApi (HTTP) trên PC: http://localhost:5214\n" +
                        "2) Terminal chạy: adb reverse tcp:5214 tcp:5214\n\n" +
                        "Nếu chạy Emulator thì API phải chạy port 5214 và app sẽ tự dùng 10.0.2.2.",
                        "OK");
                }
            }

            // 2. Kiểm tra quyền GPS
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            // 3. Đồng bộ dữ liệu mới từ API (chạy ngầm)
            _ = Task.Run(async () => {
                try
                {
                    await _apiService.SyncPOIsToLocalAsync(_databaseService, languageCode);
                    await _geofenceService.InitAsync();

                    // Ép giao diện cập nhật sau khi tải xong từ Web
                    MainThread.BeginInvokeOnMainThread(() => {
                        UpdateUIList();
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Sync error: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Appearing Error: {ex.Message}");
        }
    }

    private async Task<string> EnsureNarrationLanguageSelectedAsync()
    {
        if (AppPreferences.HasNarrationLanguageCode())
        {
            return AppPreferences.GetNarrationLanguageCode();
        }

        var options = SupportedLanguages.AllLanguages
            .Where(code => SupportedLanguages.LanguageNames.ContainsKey(code))
            .Select(code => SupportedLanguages.LanguageNames[code])
            .ToArray();

        var choice = await DisplayActionSheet(
            "Chọn ngôn ngữ thuyết minh",
            "Huỷ",
            null,
            options);

        var selectedCode = SupportedLanguages.Vietnamese;
        if (!string.IsNullOrWhiteSpace(choice))
        {
            var match = SupportedLanguages.LanguageNames.FirstOrDefault(kvp => string.Equals(kvp.Value, choice, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(match.Key))
            {
                selectedCode = match.Key;
            }
        }

        AppPreferences.SetNarrationLanguageCode(selectedCode);
        return selectedCode;
    }

    private void UpdateUIList()
    {
        var pois = _geofenceService.GetPOIs();
        listPOIs.ItemsSource = null;
        listPOIs.ItemsSource = pois;

        if (pois.Any())
        {
            lblActivePOI.Text = $"Đã tìm thấy {pois.Count} địa điểm";
            frameActivePOI.BackgroundColor = Colors.LightBlue;
        }
    }

    private void OnToggleTrackingClicked(object sender, EventArgs e)
    {
        if (!_isTrackingActive)
        {
            _locationService.StartTracking();
            btnToggleTracking.Text = "Ngừng theo dõi";
            btnToggleTracking.BackgroundColor = Colors.Red;
            _isTrackingActive = true;
        }
        else
        {
            _locationService.StopTracking();
            btnToggleTracking.Text = "Bắt đầu theo dõi";
            btnToggleTracking.BackgroundColor = Color.FromArgb("#512BD4");
            _isTrackingActive = false;
            lblUserLocation.Text = "Đã ngừng tìm vị trí.";
        }
    }

    private void OnLocationUpdated(object? sender, Microsoft.Maui.Devices.Sensors.Location location)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            lblUserLocation.Text = $"Vĩ độ: {location.Latitude:F6}, Kinh độ: {location.Longitude:F6}";

            await _geofenceService.CheckProximity(location);

            var pois = _geofenceService.GetPOIs();
            listPOIs.ItemsSource = null;
            listPOIs.ItemsSource = pois;

            if (_geofenceService.ActivePOI != null)
            {
                var active = _geofenceService.ActivePOI;
                lblActivePOI.Text = $"ĐIỂM ĐẾN: {active.Name}";
                frameActivePOI.BackgroundColor = active.IsCurrentlyPlaying ? Colors.Orange : Colors.LightGreen;
            }
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _locationService.StopTracking();
    }
}
