using TouristGuideApp.Views;
using TouristGuideApp.Services;

namespace TouristGuideApp;

public partial class App : Application
{
    private MapPage? _mapPage;
    private MainPage? _mainPage;
    private SettingsPage? _settingsPage;
    private Exception? _initException;
    private readonly IAuthService? _authService;
    private readonly LoginPage? _loginPage;
    private readonly IApiService? _apiService;

    public App(MapPage mapPage, MainPage mainPage, SettingsPage settingsPage, IAuthService authService, LoginPage loginPage, IApiService apiService)
    {
        try
        {
            InitializeComponent();
            _mapPage = mapPage;
            _mainPage = mainPage;
            _settingsPage = settingsPage;
            _authService = authService;
            _loginPage = loginPage;
            _apiService = apiService;

            StartHeartbeat();
        }
        catch (Exception ex)
        {
            _initException = ex;
            System.Diagnostics.Debug.WriteLine($"[FATAL in App Constructor]: {ex}");
        }
    }

    private void StartHeartbeat()
    {
        if (_apiService == null) return;
        
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    await _apiService.SendHeartbeatAsync();
                }
                catch { /* Ignore */ }
                await Task.Delay(TimeSpan.FromSeconds(30));
            }
        });
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        if (_initException != null)
        {
            return new Window(new ContentPage {
                Content = new ScrollView {
                    Content = new Label {
                        Text = $"APP INIT FAILED:\n{_initException}",
                        TextColor = Colors.Red,
                        FontAttributes = FontAttributes.Bold,
                        Margin = new Thickness(20, 50, 20, 20)
                    }
                }
            });
        }

        try
        {
            // Create Shell regardless as it's needed for navigation even from LoginPage
            var shell = new AppShell(_mapPage!, _mainPage!, _settingsPage!);
            
            // Initial check: if not logged in, show LoginPage
            if (_authService == null || !_authService.IsLoggedIn)
            {
                // We wrap LoginPage in a NavigationPage to allow pushing RegisterPage
                return new Window(new NavigationPage(_loginPage!));
            }

            return new Window(shell);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FATAL in App.CreateWindow]: {ex}");
            var errorPage = new ContentPage { Content = new Label { Text = "FATAL: " + ex.ToString() } };
            return new Window(errorPage);
        }
    }
}
