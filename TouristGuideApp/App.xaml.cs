using TouristGuideApp.Views;

namespace TouristGuideApp;

public partial class App : Application
{
    private readonly MapPage _mapPage;
    private readonly MainPage _mainPage;
    private readonly SettingsPage _settingsPage;

    public App(MapPage mapPage, MainPage mainPage, SettingsPage settingsPage)
    {
        InitializeComponent();
        _mapPage = mapPage;
        _mainPage = mainPage;
        _settingsPage = settingsPage;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Show splash page first, then switch to AppShell after 2.2 seconds
        var splash = new SplashPage();
        var window = new Window(splash);

        // After delay, switch to main shell
        Task.Run(async () =>
        {
            await Task.Delay(2200);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                window.Page = new AppShell(_mapPage, _mainPage, _settingsPage);
            });
        });

        return window;
    }
}
