using TouristGuideApp.Views;

namespace TouristGuideApp;

public partial class AppShell : Shell
{
    public AppShell(MapPage mapPage, MainPage mainPage, SettingsPage settingsPage)
    {
        InitializeComponent();

        // Gán các Page đã được DI khởi tạo vào đúng vị trí trong Shell
        mapContent.Content = mapPage;
        listContent.Content = mainPage;
        settingsContent.Content = settingsPage;

        // Register routes explicitly
        Routing.RegisterRoute(nameof(POIDetailsPage), typeof(POIDetailsPage));
    }
}
