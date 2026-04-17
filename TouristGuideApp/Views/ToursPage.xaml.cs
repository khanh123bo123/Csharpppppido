using TouristGuideApp.Models;
using TouristGuideApp.Services;

namespace TouristGuideApp.Views;

public partial class ToursPage : ContentPage
{
    private readonly IApiService _apiService;
    private bool _isBusy;

    public new bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public ToursPage(IApiService apiService)
    {
        InitializeComponent();
        _apiService = apiService;
        BindingContext = this;

        RefreshTours.Command = new Command(async () => await LoadToursAsync());
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadToursAsync();
    }

    private async Task LoadToursAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            var tours = await _apiService.GetToursAsync();
            ToursList.ItemsSource = tours.Where(t => t.IsActive).ToList();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Lỗi", "Không thể tải danh sách lộ trình.", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void OnTourTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is Tour tour)
        {
            try
            {
                var locations = await _apiService.GetTourLocationsAsync(tour.Id);
                
                if (locations == null || !locations.Any())
                {
                    await DisplayAlertAsync("Thông báo", "Lộ trình này chưa có địa điểm nào.", "OK");
                    return;
                }

                // In a full implementation, you would pass these locations to MapPage
                // Here we just display a summary alert and optionally go to the first POI
                var firstItem = locations.FirstOrDefault();
                
                bool openFirst = await DisplayAlertAsync("Chi tiết Lộ Trình", 
                    $"{tour.Name}\nSố điểm đến: {locations.Count}\nKhoảng cách: {tour.EstimatedDistanceKm} km\nBạn có muốn mở điểm đến đầu tiên không?", 
                    "Có", "Không");

                if (openFirst && firstItem != null)
                {
                    await Task.Delay(100);
                    await Shell.Current.GoToAsync($"{nameof(TourMapPage)}?tourId={tour.Id}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigate Error: {ex}");
            }
        }
    }
}
