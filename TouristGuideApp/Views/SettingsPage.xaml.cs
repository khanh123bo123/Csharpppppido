using System.Globalization;
using TouristGuideApp.Services;
using SupportedLanguages = TouristGuideApp.Services.SupportedLanguages;

namespace TouristGuideApp.Views;

public partial class SettingsPage : ContentPage
{
    private readonly IGeofenceService _geofenceService;
    private readonly IApiService _apiService;
    private readonly IDatabaseService _databaseService;
    private readonly IAudioService _audioService;
    private bool _isInitializing;

    public SettingsPage(IGeofenceService geofenceService, IApiService apiService, IDatabaseService databaseService, IAudioService audioService)
    {
        InitializeComponent();
        _geofenceService = geofenceService;
        _apiService = apiService;
        _databaseService = databaseService;
        _audioService = audioService;

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
        LocalizationResourceManager.Instance.SetCulture(new CultureInfo(match.Key));

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
        var files = _audioService.GetCachedAudioFiles();
        if (files.Count == 0)
        {
            await DisplayAlert("Ch?a c? file", "Hi?n ch?a c? file MP3 offline n?o ?? xo?.", "OK");
            return;
        }

        var selected = await DisplayActionSheet(
            "Ch?n MP3 ?? xo?",
            "Hu?",
            null,
            files.Select(Path.GetFileName).ToArray());

        if (string.IsNullOrWhiteSpace(selected) || selected == "Hu?")
        {
            return;
        }

        var confirm = await DisplayAlert(
            "X?c nh?n xo?",
            $"B?n c? ch?c mu?n xo? file '{selected}' kh?i m?y kh?ng?",
            "Xo?", "Hu?");

        if (!confirm)
        {
            return;
        }

        var deleted = await _audioService.DeleteCachedAudioFilesAsync(new[] { selected });
        await DisplayAlert(
            "Th?nh c?ng",
            deleted > 0 ? $"?? xo? {deleted} file MP3 offline." : "Kh?ng xo? ???c file ?? ch?n.",
            LocalizationResourceManager.Instance["Alert_OK"]);
    }
}
