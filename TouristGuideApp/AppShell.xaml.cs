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
        
        toursContent.Route = nameof(ToursPage);
        toursContent.ContentTemplate = new DataTemplate(typeof(ToursPage));

        qrScanContent.Route = nameof(QRScanPage);
        qrScanContent.ContentTemplate = new DataTemplate(typeof(QRScanPage));

        settingsContent.Route = nameof(SettingsPage);
        settingsContent.Content = settingsPage;

        // Register routes explicitly
        Routing.RegisterRoute(nameof(POIDetailsPage), typeof(POIDetailsPage));
        Routing.RegisterRoute(nameof(ToursPage), typeof(ToursPage));
        Routing.RegisterRoute(nameof(QRScanPage), typeof(QRScanPage));
        Routing.RegisterRoute(nameof(TourMapPage), typeof(TourMapPage));
    }
}
