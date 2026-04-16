using System.Globalization;
using TouristGuideApp.Models;
using TouristGuideApp.Services;

namespace TouristGuideApp.Views;

public partial class SettingsPage : ContentPage
{
    private readonly IGeofenceService _geofenceService;
    private readonly IApiService _apiService;
    private readonly IDatabaseService _databaseService;
    private bool _isInitializing;

    public SettingsPage(IGeofenceService geofenceService, IApiService apiService, IDatabaseService databaseService)
    {
        InitializeComponent();
        _geofenceService = geofenceService;
        _apiService = apiService;
        _databaseService = databaseService;

        var items = SupportedLanguages.AllLanguages
            .Where(code => SupportedLanguages.LanguageNames.ContainsKey(code))
            .Select(code => SupportedLanguages.LanguageNames[code])
            .ToList();

        pickerLanguage.ItemsSource = items;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _isInitializing = true;
        try
        {
            var code = AppPreferences.GetNarrationLanguageCode();
            if (SupportedLanguages.LanguageNames.TryGetValue(code, out var name))
            {
                pickerLanguage.SelectedItem = name;
            }
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private async void OnLanguageSelectedIndexChanged(object sender, EventArgs e)
    {
        if (_isInitializing) return;

        var selectedName = pickerLanguage.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(selectedName)) return;

        var match = SupportedLanguages.LanguageNames.FirstOrDefault(kvp => string.Equals(kvp.Value, selectedName, StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(match.Key)) return;

        var current = AppPreferences.GetNarrationLanguageCode();
        if (string.Equals(current, match.Key, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        AppPreferences.SetNarrationLanguageCode(match.Key);
        await _geofenceService.SetLanguageAsync(match.Key);
        
        // Cập nhật Culture của UI
        LocalizationResourceManager.Instance.SetCulture(new CultureInfo(match.Key));

        // Re-sync POIs using selected language pack (store localized text offline for QR/TTS)
        _ = Task.Run(async () =>
        {
            await _apiService.SyncPOIsToLocalAsync(_databaseService, match.Key);
            await _geofenceService.InitAsync();
        });

        await DisplayAlert(
            LocalizationResourceManager.Instance["Alert_LanguageChanged_Title"],
            LocalizationResourceManager.Instance["Alert_LanguageChanged_Desc"],
            LocalizationResourceManager.Instance["Alert_OK"]);
    }

    private async void OnClearHistoryClicked(object sender, EventArgs e)
    {
        // Placeholder cho chức năng xóa lịch sử (reset HasBeenPlayed)
        await DisplayAlert(
            LocalizationResourceManager.Instance["Alert_ClearHistory_Title"],
            LocalizationResourceManager.Instance["Alert_ClearHistory_Desc"],
            LocalizationResourceManager.Instance["Alert_OK"]);
    }
}
