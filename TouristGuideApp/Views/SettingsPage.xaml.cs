using System.Globalization;
using TouristGuideApp.Models;
using TouristGuideApp.Services;

namespace TouristGuideApp.Views;

public partial class SettingsPage : ContentPage
{
    private readonly IGeofenceService _geofenceService;
    private readonly IApiService _apiService;
    private readonly IDatabaseService _databaseService;
    private readonly IAuthService _authService;
    private readonly IUpdateService _updateService;
    private bool _isInitializing;

    public SettingsPage(IGeofenceService geofenceService, IApiService apiService, IDatabaseService databaseService, IAuthService authService, IUpdateService updateService)
    {
        InitializeComponent();
        _geofenceService = geofenceService;
        _apiService = apiService;
        _databaseService = databaseService;
        _authService = authService;
        _updateService = updateService;

        var items = SupportedLanguages.AllLanguages
            .Where(code => SupportedLanguages.LanguageNames.ContainsKey(code))
            .Select(code => SupportedLanguages.LanguageNames[code])
            .ToList();

        pickerLanguage.ItemsSource = items;

        // Keep API server settings visible in development phase even in release builds
        ApiServerSectionLabel.IsVisible = true;
        ApiServerSectionCard.IsVisible = true;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _isInitializing = true;
        try
        {
            UserEmailLabel.Text = _authService.IsLoggedIn 
                ? string.Format(LocalizationResourceManager.Instance["Label_LoggedInAs"], _authService.UserEmail)
                : LocalizationResourceManager.Instance["Label_NotLoggedIn"];

            ApiBaseUrlEntry.Text = _apiService.BaseAddress;

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

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlertAsync("Đăng xuất", "Bạn có chắc chắn muốn đăng xuất không?", "Đồng ý", "Hủy");
        if (confirm)
        {
            await _authService.LogoutAsync();
            
            // Switch back to LoginPage as root
            // We need the LoginPage instance. Since we don't have it easily here, 
            // we use the Application.Current.MainPage switch logic.
            // Note: In a production app, use a MessagingCenter or a dedicated NavigationService.
            if (Application.Current?.Windows?.Count > 0 && App.Current?.Handler?.MauiContext != null)
            {
                var serviceProvider = App.Current.Handler.MauiContext.Services;
                var loginPage = (LoginPage?)serviceProvider.GetService(typeof(LoginPage));
                if (loginPage != null)
                {
                    Application.Current.Windows[0].Page = new NavigationPage(loginPage);
                }
            }
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
        try
        {
            await _apiService.SyncPOIsToLocalAsync(_databaseService, match.Key);
            await _geofenceService.InitAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Sync error during language change: {ex.Message}");
        }

        await DisplayAlertAsync(
            LocalizationResourceManager.Instance["Alert_LanguageChanged_Title"],
            LocalizationResourceManager.Instance["Alert_LanguageChanged_Desc"],
            LocalizationResourceManager.Instance["Alert_OK"]);
    }

    private async void OnClearHistoryClicked(object sender, EventArgs e)
    {
        // Placeholder cho chức năng xóa lịch sử (reset HasBeenPlayed)
        await DisplayAlertAsync(
            LocalizationResourceManager.Instance["Alert_ClearHistory_Title"],
            LocalizationResourceManager.Instance["Alert_ClearHistory_Desc"],
            LocalizationResourceManager.Instance["Alert_OK"]);
    }

    private async void OnSaveApiBaseUrlClicked(object sender, EventArgs e)
    {
        var input = ApiBaseUrlEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            await DisplayAlertAsync("Lỗi", "Vui lòng nhập URL API.", "OK");
            return;
        }

        if (!input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            input = "http://" + input;
        }

        try
        {
            _apiService.SetBaseAddress(input);
            ApiBaseUrlEntry.Text = _apiService.BaseAddress;
            await DisplayAlertAsync("Thành công", $"Đã lưu máy chủ API: {_apiService.BaseAddress}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Lỗi", $"URL API không hợp lệ: {ex.Message}", "OK");
        }
    }

    private async void OnTestApiConnectionClicked(object sender, EventArgs e)
    {
        try
        {
            var ok = await _apiService.PingAsync();
            if (ok)
            {
                await DisplayAlertAsync("Kết nối OK", $"Đã kết nối thành công tới: {_apiService.BaseAddress}", "OK");
            }
            else
            {
                await DisplayAlertAsync("Chưa kết nối", $"Không ping được API: {_apiService.BaseAddress}", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Lỗi", $"Không thể kết nối API: {ex.Message}", "OK");
        }
    }

    private async void OnUpdateAppClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlertAsync(
            LocalizationResourceManager.Instance["Alert_Update_Title"], 
            LocalizationResourceManager.Instance["Alert_Update_Confirm"], 
            LocalizationResourceManager.Instance["Alert_Yes"], 
            LocalizationResourceManager.Instance["Alert_No"]);
        if (!confirm) return;

        try
        {
            if (sender is Button btn) btn.IsEnabled = false;
            
            // Thông báo bắt đầu tải
            await DisplayAlertAsync(
                LocalizationResourceManager.Instance["Alert_Info"], 
                LocalizationResourceManager.Instance["Alert_Update_Downloading"], 
                LocalizationResourceManager.Instance["Alert_OK"]);

            bool success = await _updateService.CheckAndInstallUpdateAsync();
            if (!success)
            {
                await DisplayAlertAsync(
                    LocalizationResourceManager.Instance["Alert_Error"], 
                    LocalizationResourceManager.Instance["Alert_Update_Error"], 
                    LocalizationResourceManager.Instance["Alert_OK"]);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Lỗi", $"Lỗi cập nhật: {ex.Message}", "OK");
        }
        finally
        {
            if (sender is Button btn) btn.IsEnabled = true;
        }
    }
}
