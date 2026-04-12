namespace TouristGuideApp;

using TouristGuideApp.Services;
using TouristGuideApp.Views;
using Microsoft.Maui.Controls.Hosting;
using ZXing.Net.Maui.Controls;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		try
		{
			var builder = MauiApp.CreateBuilder();
			builder
				.UseMauiApp<App>()
				.UseMauiMaps()
                .UseBarcodeReader()
				.ConfigureFonts(fonts =>
				{
					fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
					fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				});

			// Register Services
			builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
			builder.Services.AddSingleton<IAudioService, AudioService>();
			builder.Services.AddSingleton<ILocationService, LocationService>();
			builder.Services.AddSingleton<IGeofenceService, GeofenceService>();
			builder.Services.AddSingleton<ISyncService, SyncService>();
			builder.Services.AddSingleton<IOfflineMapService, OfflineMapService>();
			builder.Services.AddSingleton<IMapHtmlGenerator, MapHtmlGenerator>();

			builder.Services.AddHttpClient<IApiService, ApiService>(client =>
			{
				// Android Emulator uses 10.0.2.2 to reach localhost on host machine
				// TourGuideApi runs HTTP on port 5214 (HTTPS uses 7098)
				client.BaseAddress = new Uri("http://10.0.2.2:5214/");
				client.Timeout = TimeSpan.FromSeconds(30);
			});

			// Register Pages
			builder.Services.AddSingleton<MainPage>();
			builder.Services.AddSingleton<MapPage>();
			builder.Services.AddSingleton<SettingsPage>();
			builder.Services.AddTransient<POIDetailsPage>();
            builder.Services.AddTransient<ToursPage>();
            builder.Services.AddTransient<QRScanPage>();
            builder.Services.AddTransient<TourMapPage>();

			return builder.Build();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"!!!FATAL ERROR during app initialization: {ex}");
			throw;
		}
	}
}
