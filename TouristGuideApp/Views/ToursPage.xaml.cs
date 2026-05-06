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
            var lang = AppPreferences.GetNarrationLanguageCode();
            var tours = await _apiService.GetToursAsync(lang);
            ToursList.ItemsSource = tours.Where(t => t.IsActive).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadTours Error: {ex.Message}");
            await DisplayAlertAsync(
                LocalizationResourceManager.Instance["Alert_Error"], 
                LocalizationResourceManager.Instance["Tour_LoadError"], 
                LocalizationResourceManager.Instance["Alert_OK"]);
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
                    await DisplayAlertAsync(
                        LocalizationResourceManager.Instance["Alert_Info"], 
                        LocalizationResourceManager.Instance["Tour_NoLocations"], 
                        LocalizationResourceManager.Instance["Alert_OK"]);
                    return;
                }

                // In a full implementation, you would pass these locations to MapPage
                // Here we just display a summary alert and optionally go to the first POI
                var firstItem = locations.FirstOrDefault();
                
                bool openFirst = await DisplayAlertAsync(
                    LocalizationResourceManager.Instance["Tour_DetailTitle"], 
                    string.Format(LocalizationResourceManager.Instance["Tour_DetailFormat"], tour.Name, locations.Count, tour.EstimatedDistanceKm), 
                    LocalizationResourceManager.Instance["Alert_Yes"], 
                    LocalizationResourceManager.Instance["Alert_No"]);

                if (openFirst && firstItem != null)
                {
                    await Task.Delay(100);
                    await Shell.Current.GoToAsync($"{nameof(TourMapPage)}?tourId={tour.Id}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigate Error: {ex.Message}");
            }
        }
    }
}
