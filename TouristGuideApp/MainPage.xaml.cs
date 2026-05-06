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
    private System.Collections.ObjectModel.ObservableCollection<POI> _poiCollection = new();

    public MainPage(ILocationService locationService, IGeofenceService geofenceService, IApiService apiService, IDatabaseService databaseService)
    {
        InitializeComponent();
        _locationService = locationService;
        _geofenceService = geofenceService;
        _apiService = apiService;
        _databaseService = databaseService;
        _locationService.LocationUpdated += OnLocationUpdated;

        listPOIs.ItemsSource = _poiCollection;
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

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            // Delay significantly to allow iOS native UI transitions to fully stabilize.
            // If popups fire during 'window.Page' swap, iOS crashes completely.
            await Task.Delay(2000);

            // First launch: choose narration language pack
            var languageCode = await EnsureNarrationLanguageSelectedAsync();
            await _geofenceService.SetLanguageAsync(languageCode);

            // 1. Hiển thị dữ liệu cũ từ SQLite ngay lập tức
            await _geofenceService.InitAsync();
            UpdateUIList();

            // Check for API reachability (delays another alert if needed)
            if (!_geofenceService.GetPOIs().Any())
            {
                using var pingCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                var canReachApi = await _apiService.PingAsync(pingCts.Token);
                if (!canReachApi)
                {
                    await Task.Delay(1000); // Avoid overlapping alerts
                    await DisplayAlertAsync(
                        LocalizationResourceManager.Instance["Alert_NoConnection"],
                        "App chưa tải được dữ liệu địa điểm (POIs).\n\n" +
                        "Nếu chạy trên điện thoại thật qua USB:\n" +
                        "1) Chạy TourGuideApi (HTTP) trên PC: http://localhost:5214\n" +
                        "2) Terminal chạy: adb reverse tcp:5214 tcp:5214\n\n" +
                        "Nếu chạy Emulator thì API phải chạy port 5214 và app sẽ tự dùng 10.0.2.2.",
                        LocalizationResourceManager.Instance["Alert_OK"]);
                }
            }

            // 2. Kiểm tra quyền GPS (Delay to ensure no two native popovers overlap)
            await Task.Delay(1000);
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

        // DELAY GLOBALLY to fix iOS native CoreFoundation / UIKit crashes.
        // Calling DisplayActionSheet or Alert too early inside OnAppearing when the root View 
        // Controller is newly spawned crashes iOS inside xamarin_UIApplicationMain
        await Task.Delay(800); 

        var choice = await DisplayActionSheetAsync(
            LocalizationResourceManager.Instance["Alert_LanguageTitle"],
            LocalizationResourceManager.Instance["Alert_Cancel"], 
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

    private string _currentCategory = "All";

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateUIList(e.NewTextValue, _currentCategory);
    }

    private void OnCategoryTapped(object sender, TappedEventArgs e)
    {
        _currentCategory = e.Parameter as string ?? "All";
        UpdateUIList(searchBar.Text ?? "", _currentCategory);
        
        // Highlight selected category visually
        if (sender is Border border && border.Parent is HorizontalStackLayout stack)
        {
            foreach (var child in stack.Children)
            {
                if (child is Border otherBorder)
                {
                    bool isSelected = otherBorder == border;
                    otherBorder.BackgroundColor = isSelected ? Color.FromArgb("#B84A39") : Colors.White;
                    otherBorder.Stroke = isSelected ? Colors.Transparent : Color.FromArgb("#EAE3D9");
                    if (otherBorder.Content is Label label)
                    {
                        label.TextColor = isSelected ? Colors.White : Color.FromArgb("#555");
                        label.FontAttributes = isSelected ? FontAttributes.Bold : FontAttributes.None;
                    }
                }
            }
        }
    }

    private void UpdateUIList(string searchText = "", string category = "All")
    {
        var pois = _geofenceService.GetPOIs();

        // 1. Search text filter (Name, Category, Description)
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            pois = pois.Where(p =>
                    (p.Name?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.Category?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.Description?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }

        // 2. Category selection filter (with mapping for local data)
        if (category != "All")
        {
            var matchTerms = GetCategoryTerms(category);
            pois = pois.Where(p => 
                matchTerms.Any(term => p.Category?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
        }

        var normalizedPois = pois
            .Where(p => p != null)
            .Select(p =>
            {
                p.ImageUrl = NormalizeImageUrl(p.ImageUrl);
                return p;
            })
            .ToList();

        // Recreate the ObservableCollection to avoid stale UI state on iOS CollectionView.
        _poiCollection = new System.Collections.ObjectModel.ObservableCollection<POI>(normalizedPois);
        listPOIs.ItemsSource = _poiCollection;

        if (normalizedPois.Any())
        {
            lblActivePOI.Text = string.IsNullOrWhiteSpace(searchText) && category == "All" 
                ? string.Format(LocalizationResourceManager.Instance["POI_FoundCount"], normalizedPois.Count)
                : string.Format(LocalizationResourceManager.Instance["POI_FilterCount"], normalizedPois.Count);
            frameActivePOI.BackgroundColor = Color.FromArgb("#2C4C3B");
        }
        else
        {
            lblActivePOI.Text = LocalizationResourceManager.Instance["POI_NoneFound"];
            frameActivePOI.BackgroundColor = Colors.Gray;
        }
    }

    private List<string> GetCategoryTerms(string categoryKey)
    {
        return categoryKey switch
        {
            "Restaurant" => new List<string> { "Nhà hàng", "Hải sản", "Quán ăn", "Ốc", "Lẩu", "Cơm", "Phở", "Hủ tiếu" },
            "Cafe" => new List<string> { "Cà phê", "Cafe", "Giải khát", "Chè", "Nước ép", "Trà" },
            "Historic" => new List<string> { "Di tích", "Lịch sử", "Chùa", "Nhà thờ", "Bảo tàng", "Công trình" },
            "Shopping" => new List<string> { "Mua sắm", "Chợ", "Cửa hàng", "Siêu thị", "Quà lưu niệm" },
            _ => new List<string> { categoryKey }
        };
    }

    private string NormalizeImageUrl(string? rawUrl)
    {
        var absolute = HtmlUtils.EnsureAbsoluteUrl(rawUrl, _apiService.BaseAddress);
        if (string.IsNullOrWhiteSpace(absolute))
        {
            return string.Empty;
        }

        return Uri.TryCreate(absolute, UriKind.Absolute, out _) ? absolute : string.Empty;
    }

    private void OnToggleTrackingClicked(object sender, EventArgs e)
    {
        if (!_isTrackingActive)
        {
            _locationService.StartTracking();
            btnToggleTracking.Text = LocalizationResourceManager.Instance["POI_StopTracking"];
            btnToggleTracking.BackgroundColor = Color.FromArgb("#8B3E36");
            _isTrackingActive = true;
        }
        else
        {
            _locationService.StopTracking();
            btnToggleTracking.Text = LocalizationResourceManager.Instance["POI_StartTracking"];
            btnToggleTracking.BackgroundColor = Color.FromArgb("#B84A39");
            _isTrackingActive = false;
        }
    }

    private void OnLocationUpdated(object? sender, Microsoft.Maui.Devices.Sensors.Location location)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await _geofenceService.CheckProximity(location);

            var pois = _geofenceService.GetPOIs();
            // Re-apply filter if active
            UpdateUIList(searchBar.Text);

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
