using TouristGuideApp.Services;

namespace TouristGuideApp.Views;

public partial class LoginPage : ContentPage
{
    private readonly IAuthService _authService;
    private readonly IApiService _apiService;
    private readonly IServiceProvider _serviceProvider;

    public LoginPage(IAuthService authService, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _authService = authService;
        _serviceProvider = serviceProvider;
        _apiService = _serviceProvider.GetRequiredService<IApiService>();

#if !DEBUG
        ApiConfigLabel.IsVisible = false;
#endif
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var email = EmailEntry.Text?.Trim();
        var password = PasswordEntry.Text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            await DisplayAlertAsync("Lỗi", "Vui lòng nhập đầy đủ Email và Mật khẩu.", "OK");
            return;
        }

        LoadingIndicator.IsRunning = true;
        ((Button)sender).IsEnabled = false;

        try
        {
            var (success, errorMessage) = await _authService.LoginWithDetailsAsync(email, password);
            if (success)
            {
                // Switch back to AppShell as root
                var shell = _serviceProvider.GetRequiredService<AppShell>();
                if (Application.Current != null && Application.Current.Windows.Count > 0)
                {
                    Application.Current.Windows[0].Page = shell;
                }
            }
            else
            {
                var message = string.IsNullOrWhiteSpace(errorMessage)
                    ? "Email hoặc mật khẩu không chính xác."
                    : errorMessage;

                if (message.Contains("Invalid email or password", StringComparison.OrdinalIgnoreCase))
                {
                    message = "Sai email hoặc mật khẩu trên hệ tài khoản mobile.\nNếu bạn chỉ đăng ký ở website quản trị, hãy đăng ký lại trong app mobile.";
                }

                await DisplayAlertAsync("Thất bại", message, "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Lỗi hệ thống", "Có lỗi xảy ra: " + ex.Message, "OK");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            ((Button)sender).IsEnabled = true;
        }
    }

    private async void OnRegisterTapped(object sender, TappedEventArgs e)
    {
        var registerPage = _serviceProvider.GetRequiredService<RegisterPage>();
        await Navigation.PushAsync(registerPage);
    }

    private async void OnConfigureApiTapped(object sender, TappedEventArgs e)
    {
        var input = await DisplayPromptAsync(
            "Cấu hình API",
            "Nhập URL máy chủ (ví dụ: http://192.168.x.x:5214/)",
            accept: "Lưu",
            cancel: "Hủy",
            initialValue: _apiService.BaseAddress,
            keyboard: Keyboard.Url);

        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        var normalized = input.Trim();
        if (!normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "http://" + normalized;
        }

        try
        {
            _apiService.SetBaseAddress(normalized);
            var ok = await _apiService.PingAsync();
            await DisplayAlertAsync(
                ok ? "Kết nối OK" : "Chưa kết nối",
                ok
                    ? $"Đã kết nối thành công tới: {_apiService.BaseAddress}"
                    : $"Đã lưu URL nhưng chưa ping được: {_apiService.BaseAddress}",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", $"URL API không hợp lệ: {ex.Message}", "OK");
        }
    }

    private async void OnSkipLoginClicked(object sender, EventArgs e)
    {
        // Switch to AppShell as root (Guest Mode)
        var shell = _serviceProvider.GetRequiredService<AppShell>();
        if (Application.Current != null && Application.Current.Windows.Count > 0)
        {
            Application.Current.Windows[0].Page = shell;
        }
    }
}
