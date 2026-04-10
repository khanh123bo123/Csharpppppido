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
        return new Window(new AppShell(_mapPage, _mainPage, _settingsPage));
    }
}
