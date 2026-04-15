namespace TouristGuideApp;

using TouristGuideApp.Services;
using TouristGuideApp.Views;
using Microsoft.Maui.Controls.Hosting;
using ZXing.Net.Maui.Controls;

public static class MauiProgram
{
	private const int DefaultApiPort = 5214;

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
				// TourGuideApi runs HTTP on port 5214 (HTTPS uses 7098)
				// Android:
				// - Emulator: use 10.0.2.2 to reach host machine localhost
				// - Physical device over USB (adb reverse): use 127.0.0.1
				client.BaseAddress = ResolveApiBaseAddress();
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

	private static Uri ResolveApiBaseAddress()
	{
#if ANDROID
		var isEmulator = Microsoft.Maui.Devices.DeviceInfo.Current.DeviceType == Microsoft.Maui.Devices.DeviceType.Virtual;
		var host = isEmulator ? "10.0.2.2" : "127.0.0.1";
		return new Uri($"http://{host}:{DefaultApiPort}/");
#else
		return new Uri($"http://localhost:{DefaultApiPort}/");
#endif
	}
}