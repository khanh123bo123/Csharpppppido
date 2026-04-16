using Microsoft.Maui.Controls;
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
            // 1. Hiển thị dữ liệu cũ từ SQLite ngay lập tức
            await _geofenceService.InitAsync();
            UpdateUIList();

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
                    await _apiService.SyncPOIsToLocalAsync(_databaseService);
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

    private void UpdateUIList()
    {
        var pois = _geofenceService.GetPOIs();
        listPOIs.ItemsSource = null;
        listPOIs.ItemsSource = pois;

        if (pois.Any())
        {
            lblActivePOI.Text = $"Đã tìm thấy {pois.Count} địa điểm";
            frameActivePOI.BackgroundColor = Color.FromArgb("#2C4C3B");
        }
    }

    private void OnToggleTrackingClicked(object sender, EventArgs e)
    {
        if (!_isTrackingActive)
        {
            _locationService.StartTracking();
            btnToggleTracking.Text = "⏹  Ngừng theo dõi";
            btnToggleTracking.BackgroundColor = Color.FromArgb("#8B3E36");
            _isTrackingActive = true;
        }
        else
        {
            _locationService.StopTracking();
            btnToggleTracking.Text = "📡  Bắt đầu theo dõi";
            btnToggleTracking.BackgroundColor = Color.FromArgb("#B84A39");
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
                lblActivePOI.Text = $"{active.Name}";
                frameActivePOI.BackgroundColor = active.IsCurrentlyPlaying
                    ? Color.FromArgb("#B84A39")   // Terracotta khi đang phát
                    : Color.FromArgb("#2C4C3B");   // Green khi gần
            }
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _locationService.StopTracking();
    }
}
