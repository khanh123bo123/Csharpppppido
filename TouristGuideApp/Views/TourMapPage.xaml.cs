using TouristGuideApp.Models;
using TouristGuideApp.Services;

namespace TouristGuideApp.Views;

[QueryProperty(nameof(TourId), "tourId")]
public partial class TourMapPage : ContentPage
{
    private readonly IApiService _apiService;
    private readonly IMapHtmlGenerator _mapHtmlGenerator;
    private string _tourId = string.Empty;

    public string TourId
    {
        get => _tourId;
        set
        {
            _tourId = value;
            OnPropertyChanged();
            _ = LoadTourMapAsync(value);
        }
    }

    public TourMapPage(IApiService apiService, IMapHtmlGenerator mapHtmlGenerator)
    {
        InitializeComponent();
        _apiService = apiService;
        _mapHtmlGenerator = mapHtmlGenerator;
    }

    private async Task LoadTourMapAsync(string tourIdStr)
    {
        if (!int.TryParse(tourIdStr, out int tourId)) return;

        try
        {
            // Get tour info
            var tours = await _apiService.GetToursAsync();
            var tour = tours.FirstOrDefault(t => t.Id == tourId);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (tour != null)
                {
                    lblTourName.Text = tour.Name;
                    lblTourInfo.Text = $"{tour.EstimatedDistanceKm} km · {tour.EstimatedDurationMinutes} phút";
                }
            });

            // Get tour locations
            var tourLocations = await _apiService.GetTourLocationsAsync(tourId);

            if (tourLocations == null || !tourLocations.Any())
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlertAsync("Thông báo", "Lộ trình này chưa có điểm dừng nào.", "OK");
                });
                return;
            }

            // Fetch each location detail and convert to POI
            var pois = new List<POI>();
            foreach (var tl in tourLocations.OrderBy(x => x.OrderIndex))
            {
                var location = await _apiService.GetLocationAsync(tl.LocationId);
                if (location != null)
                {
                    pois.Add(new POI
                    {
                        Id = location.Id,
                        Name = location.Name ?? "Chưa đặt tên",
                        Description = location.Description ?? "",
                        Latitude = location.Latitude,
                        Longitude = location.Longitude,
                        Category = location.Category ?? "",
                        Address = location.Address,
                        PhoneNumber = location.PhoneNumber,
                        ImageUrl = location.ImageUrl,
                        AudioUrl = location.AudioUrl
                    });
                }
            }

            // Get user location
            double userLat = 10.7769, userLon = 106.7009;
            try
            {
                var userPos = await Geolocation.Default.GetLocationAsync(new GeolocationRequest
                {
                    DesiredAccuracy = GeolocationAccuracy.Medium,
                    Timeout = TimeSpan.FromSeconds(5)
                });
                if (userPos != null) { userLat = userPos.Latitude; userLon = userPos.Longitude; }
            }
            catch { }

            // Generate tour map HTML with route
            string html = _mapHtmlGenerator.GenerateTourMapHtml(tour?.Name ?? "Lộ trình", pois, userLat, userLon);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                tourMapWebView.Source = new HtmlWebViewSource { Html = html };
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TourMapPage Error: {ex}");
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlertAsync("Lỗi", "Không thể tải bản đồ lộ trình.", "OK");
            });
        }
    }
}
