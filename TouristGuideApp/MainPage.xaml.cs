using Microsoft.Maui.Controls;
using TouristGuideApp.Services;

namespace TouristGuideApp;

public partial class MainPage : ContentPage
{
    private readonly ILocationService _locationService;
    private readonly IGeofenceService _geofenceService;
    private bool _isTrackingActive = false;

    public MainPage(ILocationService locationService, IGeofenceService geofenceService)
    {
        InitializeComponent();
        _locationService = locationService;
        _geofenceService = geofenceService;

        // Đăng ký sự kiện cập nhật vị trí từ GPS
        _locationService.LocationUpdated += OnLocationUpdated;
    }

    private async void OnPOITapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is TouristGuideApp.Models.POI selectedPOI)
        {
            // Hiển thị thông tin chi tiết
            bool listen = await DisplayAlert(selectedPOI.Name,
                $"{selectedPOI.Description}\n\nKhoảng cách kích hoạt: {selectedPOI.Radius}m",
                "Nghe thuyết minh", "Đóng");

            if (listen)
            {
                // Cho phép nghe thuyết minh ngay lập tức khi click thủ công (bỏ qua cooldown)
                await _geofenceService.PlaySpeechAsync(selectedPOI, ignoreCooldown: true);
            }
        }
    }

    private async void OnPOISelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is TouristGuideApp.Models.POI selectedPOI)
        {
            // Reset selection so the same item can be clicked again
            ((CollectionView)sender).SelectedItem = null;

            // Hiển thị thông tin chi tiết
            bool listen = await DisplayAlert(selectedPOI.Name,
                $"{selectedPOI.Description}\n\nKhoảng cách kích hoạt: {selectedPOI.Radius}m",
                "Nghe thuyết minh", "Đóng");

            if (listen)
            {
                // Cho phép nghe thuyết minh ngay lập tức khi click
                await _geofenceService.CheckProximity(new Microsoft.Maui.Devices.Sensors.Location(selectedPOI.Latitude, selectedPOI.Longitude));
            }
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Khởi tạo dữ liệu từ SQLite
        await _geofenceService.InitAsync();
        listPOIs.ItemsSource = _geofenceService.GetPOIs();
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
            lblActivePOI.Text = "Không có điểm nào gần đây";
            frameActivePOI.BackgroundColor = Colors.LightGray;
        }
    }

    private async void OnLocationUpdated(object? sender, Microsoft.Maui.Devices.Sensors.Location location)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            lblUserLocation.Text = $"Vĩ độ: {location.Latitude:F6}, Kinh độ: {location.Longitude:F6}";

            await _geofenceService.CheckProximity(location);

            if (_geofenceService.ActivePOI != null)
            {
                var active = _geofenceService.ActivePOI;
                lblActivePOI.Text = $"ĐIỂM ĐẾN: {active.Name}";

                // Hiển thị trạng thái âm thanh
                if (active.IsCurrentlyPlaying)
                {
                    lblActivePOI.Text += " (Đang thuyết minh...)";
                    frameActivePOI.BackgroundColor = Colors.Orange;
                }
                else
                {
                    frameActivePOI.BackgroundColor = Colors.LightGreen;
                }
            }
            else
            {
                lblActivePOI.Text = "Không có điểm nào gần đây";
                frameActivePOI.BackgroundColor = Colors.LightGray;
            }
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _locationService.StopTracking();
    }
}
