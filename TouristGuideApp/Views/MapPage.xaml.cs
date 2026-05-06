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
    private readonly IApiService _apiService;
    private readonly IDatabaseService _databaseService;
    private readonly IAudioService _audioService;
    private readonly IAuthService _authService;

    public MapPage(ILocationService locationService, IGeofenceService geofenceService, IOfflineMapService offlineMapService, IMapHtmlGenerator mapHtmlGenerator, IApiService apiService, IDatabaseService databaseService, IAudioService audioService, IAuthService authService)
    {
        InitializeComponent();
        _locationService = locationService;
        _geofenceService = geofenceService;
        _offlineMapService = offlineMapService;
        _mapHtmlGenerator = mapHtmlGenerator;
        _apiService = apiService;
        _databaseService = databaseService;
        _audioService = audioService;
        _authService = authService;

        _locationService.LocationUpdated += OnLocationUpdated;
        Connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                // Delay significantly so other tabs don't clash initializing Native UI
                await Task.Delay(1000); 

                // Initialize offline map service
                await _offlineMapService.InitializeAsync();
                
                // Start tracking user location & Initialize POIs from DB
                await _geofenceService.InitAsync();
                _locationService.StartTracking();

                var currentPois = _geofenceService.GetPOIs();
                var lang = AppPreferences.GetNarrationLanguageCode();

                // If language changed globally or cache is empty, we must sync
                bool needsSync = (currentPois == null || !currentPois.Any());
                if (!needsSync && currentPois != null && currentPois.Any())
                {
                    var firstPoi = currentPois.First();
                    if (!string.Equals(firstPoi.LanguageCode, lang, StringComparison.OrdinalIgnoreCase))
                    {
                        needsSync = true;
                    }
                }

                if (needsSync)
                {
                    lblStatus.Text = LocalizationResourceManager.Instance["Map_UpdatingLang"];
                    await _apiService.SyncPOIsToLocalAsync(_databaseService, lang);
                    await _geofenceService.InitAsync();
                    currentPois = _geofenceService.GetPOIs();
                }

                // Load map (now that POIs are loaded)
                await LoadMapAsync();
                
                // Check connectivity
                UpdateConnectivityStatus();
                
                lblStatus.Text = currentPois != null && currentPois.Any() 
                    ? string.Format(LocalizationResourceManager.Instance["Map_FoundCount"], currentPois.Count) 
                    : LocalizationResourceManager.Instance["Map_NoData"];
            }
            catch (Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine($"[FATAL in MapPage.OnAppearing]: {ex}");
            }
        }

    private string _searchFilter = string.Empty;

    private async Task LoadMapAsync()
    {
        try
        {
            var allPois = _geofenceService.GetPOIs();
            var filteredPois = allPois;

            if (!string.IsNullOrWhiteSpace(_searchFilter))
            {
                var query = _searchFilter.ToLower();
                filteredPois = allPois.Where(p => 
                    (p.Name != null && p.Name.ToLower().Contains(query)) || 
                    (p.Description != null && p.Description.ToLower().Contains(query)) ||
                    (p.Address != null && p.Address.ToLower().Contains(query)) ||
                    (p.Category != null && p.Category.ToLower().Contains(query))
                ).ToList();
            }

            var userLoc = await GetCurrentLocationAsync();
            double userLat = userLoc?.Latitude ?? 10.7769;
            double userLon = userLoc?.Longitude ?? 106.7009;

            // Generate Leaflet map HTML
            string mapHtml = _mapHtmlGenerator.GenerateMapHtml(filteredPois, userLat, userLon);
            
            // Load HTML into WebView
            tourMapWebView.Source = new HtmlWebViewSource { Html = mapHtml };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading map: {ex.Message}");
            await DisplayAlertAsync(
                LocalizationResourceManager.Instance["Alert_Error"], 
                LocalizationResourceManager.Instance["Map_LoadError"], 
                LocalizationResourceManager.Instance["Alert_OK"]);
        }
    }

    private void OnSearchBarBorderTapped(object? sender, EventArgs e)
    {
        searchBar.Focus();
    }

    private async void OnSearchButtonPressed(object? sender, EventArgs e)
    {
        _searchFilter = searchBar.Text ?? string.Empty;
        await LoadMapAsync();
    }

    private async void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.NewTextValue))
        {
            _searchFilter = string.Empty;
            await LoadMapAsync();
        }
    }

    private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url.Contains("poi-app:"))
        {
            e.Cancel = true;
            
            try 
            {
                var idPart = e.Url.Split(':').LastOrDefault();
                if (int.TryParse(idPart, out int poiId))
                {
                    await OnPoiSelectedAsync(poiId);
                }
            }
            catch (Exception ex) 
            {
                System.Diagnostics.Debug.WriteLine($"POI Select Error: {ex.Message}");
            }
        }
    }

    private POI? _selectedPoi;

    private async Task OnPoiSelectedAsync(int id)
    {
        var pois = _geofenceService.GetPOIs();
        _selectedPoi = pois?.FirstOrDefault(p => p.Id == id);

        if (_selectedPoi != null)
        {
            MainThread.BeginInvokeOnMainThread(() => {
                lblQuickName.Text = _selectedPoi.Name;
                lblQuickRating.Text = _selectedPoi.AverageRating > 0 ? $"⭐ {_selectedPoi.AverageRating:F1}" : "⭐ --";
                lblQuickCategory.Text = _selectedPoi.Category ?? LocalizationResourceManager.Instance["Map_DefaultCategory"];
                lblQuickAddress.Text = _selectedPoi.Address ?? LocalizationResourceManager.Instance["Map_NoAddress"];
                
                if (!string.IsNullOrEmpty(_selectedPoi.ImageUrl))
                    imgQuickPOI.Source = _selectedPoi.ImageUrl;
                else
                    imgQuickPOI.Source = "placeholder_poi.png";

                // Force visibility without complex translation for now to ensure it works
                pnlQuickView.TranslationY = 0;
                pnlQuickView.IsVisible = true;
                pnlQuickView.Opacity = 1;
            });
        }
    }

    private void OnClosePanelClicked(object? sender, EventArgs e)
    {
        pnlQuickView.IsVisible = false;
    }

    private async void OnQuickViewDetailsClicked(object? sender, EventArgs e)
    {
        if (_selectedPoi != null)
        {
            await Navigation.PushAsync(new POIDetailsPage(_geofenceService, _apiService, _databaseService, _audioService, _authService) { POIItem = _selectedPoi });
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
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (_geofenceService.ActivePOI != null)
            {
                lblStatus.Text = $"📍 {_geofenceService.ActivePOI.Name}";
            }
            else
            {
                lblStatus.Text = LocalizationResourceManager.Instance["Map_SearchNear"];
            }

            // Update real-time position on map
            try 
            {
                await tourMapWebView.EvaluateJavaScriptAsync($"updateUserLocation({location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating map dot: {ex.Message}");
            }
        });
    }

    private async void OnCenterOnUserClicked(object sender, EventArgs e)
    {
        try 
        {
            await tourMapWebView.EvaluateJavaScriptAsync("centerOnUser()");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error centering map: {ex.Message}");
        }
    }

    private void UpdateConnectivityStatus()
    {
        var current = Connectivity.Current.NetworkAccess;
        if (current == NetworkAccess.Internet)
        {
            lblMode.Text = LocalizationResourceManager.Instance["Map_Online"];
            lblMode.TextColor = Colors.Green;
        }
        else
        {
            lblMode.Text = LocalizationResourceManager.Instance["Map_Offline"];
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
            btnCacheMap.Text = LocalizationResourceManager.Instance["Map_Caching"];

            // Cache tiles for Quận 4 area at zoom 14
            await _offlineMapService.CacheTilesAsync(
                minLat: 10.74, 
                minLon: 106.63, 
                maxLat: 10.82, 
                maxLon: 106.77, 
                zoomLevel: 14
            );

            btnCacheMap.Text = LocalizationResourceManager.Instance["Map_CacheBtn"];
            btnCacheMap.IsEnabled = true;
            
            await DisplayAlertAsync(
                LocalizationResourceManager.Instance["Alert_ClearHistory_Title"], 
                LocalizationResourceManager.Instance["Map_CacheSuccess"], 
                LocalizationResourceManager.Instance["Alert_OK"]);
        }
        catch (Exception ex)
        {
            btnCacheMap.Text = LocalizationResourceManager.Instance["Map_CacheBtn"];
            btnCacheMap.IsEnabled = true;
            
            await DisplayAlertAsync(
                LocalizationResourceManager.Instance["Alert_Error"], 
                string.Format(LocalizationResourceManager.Instance["Map_CacheError"], ex.Message), 
                LocalizationResourceManager.Instance["Alert_OK"]);
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

