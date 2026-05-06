using TouristGuideApp.Services;

namespace TouristGuideApp.Views;

public partial class RegisterPage : ContentPage
{
    private readonly IAuthService _authService;
    private readonly IApiService _apiService;

    public RegisterPage(IAuthService authService, IApiService apiService)
    {
        InitializeComponent();
        _authService = authService;
        _apiService = apiService;

#if !DEBUG
        ApiConfigLabel.IsVisible = false;
#endif
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim();
        var email = EmailEntry.Text?.Trim();
        var password = PasswordEntry.Text;
        var confirm = ConfirmPasswordEntry.Text;

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            await DisplayAlertAsync("Lỗi", "Vui lòng nhập đầy đủ thông tin.", "OK");
            return;
        }

        if (password != confirm)
        {
            await DisplayAlertAsync("Lỗi", "Mật khẩu xác nhận không khớp.", "OK");
            return;
        }

        LoadingIndicator.IsRunning = true;
        ((Button)sender).IsEnabled = false;

        try
        {
            var result = await _authService.RegisterAsync(email, password, name);
            if (result.Success)
            {
                await DisplayAlertAsync("Thành công", "Tài khoản của bạn đã được tạo. Vui lòng đăng nhập.", "OK");
                await Navigation.PopAsync(); // Go back to Login
            }
            else
            {
                await DisplayAlertAsync("Thất bại", result.ErrorMessage ?? "Email này đã được sử dụng hoặc có lỗi xảy ra.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Lỗi", "Không thể kết nối đến máy chủ: " + ex.Message, "OK");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            ((Button)sender).IsEnabled = true;
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
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
            await DisplayAlertAsync("Lỗi", $"URL API không hợp lệ: {ex.Message}", "OK");
        }
    }
}
