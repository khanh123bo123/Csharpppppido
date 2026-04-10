using Microsoft.Maui.Controls;
using TouristGuideApp.Services;
using TouristGuideApp.Models;
using Location = Microsoft.Maui.Devices.Sensors.Location;

namespace TouristGuideApp.Views;

public partial class MapPage : ContentPage
{
    private readonly ILocationService _locationService;
    private readonly IGeofenceService _geofenceService;
    private readonly IOfflineMapService _offlineMapService;
    private readonly IMapHtmlGenerator _mapHtmlGenerator;

    public MapPage(ILocationService locationService, IGeofenceService geofenceService, IOfflineMapService offlineMapService, IMapHtmlGenerator mapHtmlGenerator)
    {
        InitializeComponent();
        _locationService = locationService;
        _geofenceService = geofenceService;
        _offlineMapService = offlineMapService;
        _mapHtmlGenerator = mapHtmlGenerator;

        _locationService.LocationUpdated += OnLocationUpdated;
        Connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Initialize offline map service
        await _offlineMapService.InitializeAsync();
        
        // Load map
        await LoadMapAsync();
        
        // Start tracking user location
        await _geofenceService.InitAsync();
        _locationService.StartTracking();
        
        // Check connectivity
        UpdateConnectivityStatus();
    }

    private async Task LoadMapAsync()
    {
        try
        {
            var pois = _geofenceService.GetPOIs();
            var userLoc = await GetCurrentLocationAsync();
            double userLat = userLoc?.Latitude ?? 10.7769;
            double userLon = userLoc?.Longitude ?? 106.7009;

            // Generate Leaflet map HTML
            string mapHtml = _mapHtmlGenerator.GenerateMapHtml(pois, userLat, userLon);
            
            // Load HTML into WebView
            tourMapWebView.Source = new HtmlWebViewSource { Html = mapHtml };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading map: {ex.Message}");
            await DisplayAlertAsync("Lỗi", "Không thể load bản đồ", "OK");
        }
    }

    private async Task<Location?> GetCurrentLocationAsync()
    {
        try
        {
            var location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest
            {
                DesiredAccuracy = GeolocationAccuracy.Best,
                Timeout = TimeSpan.FromSeconds(10)
            });
            return location;
        }
        catch
        {
            return null;
        }
    }

    private void OnLocationUpdated(object? sender, Location location)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_geofenceService.ActivePOI != null)
            {
                lblStatus.Text = $"📍 {_geofenceService.ActivePOI.Name}";
            }
            else
            {
                lblStatus.Text = "Tìm kiếm nhà hàng gần bạn...";
            }
        });
    }

    private void UpdateConnectivityStatus()
    {
        var current = Connectivity.Current.NetworkAccess;
        if (current == NetworkAccess.Internet)
        {
            lblMode.Text = "Chế độ: Online ☁️";
            lblMode.TextColor = Colors.Green;
        }
        else
        {
            lblMode.Text = "Chế độ: Offline 📦 (cached tiles)";
            lblMode.TextColor = Colors.Orange;
        }
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateConnectivityStatus();
        });
    }

    /// <summary>
    /// Cache tiles for current view area (zoom level 14)
    /// Quận 4 bounds: 10.74 to 10.82 lat, 106.63 to 106.77 lon
    /// </summary>
    private async void OnCacheMapClicked(object sender, EventArgs e)
    {
        try
        {
            btnCacheMap.IsEnabled = false;
            btnCacheMap.Text = "Đang cache...";

            // Cache tiles for Quận 4 area at zoom 14
            await _offlineMapService.CacheTilesAsync(
                minLat: 10.74, 
                minLon: 106.63, 
                maxLat: 10.82, 
                maxLon: 106.77, 
                zoomLevel: 14
            );

            btnCacheMap.Text = "Cache Bản đồ";
            btnCacheMap.IsEnabled = true;
            
            await DisplayAlertAsync("Thành công", "Bản đồ đã được lưu offline", "OK");
        }
        catch (Exception ex)
        {
            btnCacheMap.Text = "Cache Bản đồ";
            btnCacheMap.IsEnabled = true;
            
            await DisplayAlertAsync("Lỗi", $"Không thể cache bản đồ: {ex.Message}", "OK");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _locationService.LocationUpdated -= OnLocationUpdated;
        Connectivity.ConnectivityChanged -= OnConnectivityChanged;
        _locationService.StopTracking();
    }
}

