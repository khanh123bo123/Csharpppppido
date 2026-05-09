namespace TouristGuideApp;

using TouristGuideApp.Services;
using TouristGuideApp.Views;
using Microsoft.Maui.Controls.Hosting;
using ZXing.Net.Maui.Controls;

public static class MauiProgram
{
	private const string ProductionApiBaseUrl = "https://sharpppio-api.azurewebsites.net/";

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
				// Default endpoint for Azure deployment. User can override in Settings.
				client.BaseAddress = ResolveApiBaseAddress();
				client.Timeout = TimeSpan.FromSeconds(30);
			});

			builder.Services.AddHttpClient<IAppHubService, AppHubService>(client =>
			{
				client.BaseAddress = ResolveApiBaseAddress();
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
		if (AppPreferences.TryGetApiBaseUrl(out var preferredApiBaseUrl) && preferredApiBaseUrl is not null)
		{
			return preferredApiBaseUrl;
		}
		if (!Uri.TryCreate(ProductionApiBaseUrl, UriKind.Absolute, out var productionUri))
		{
			throw new InvalidOperationException("Production API base URL is not configured. Set ProductionApiBaseUrl in MauiProgram.cs.");
		}

		return EnsureTrailingSlash(productionUri);
	}

	private static Uri EnsureTrailingSlash(Uri uri)
	{
		var absoluteUri = uri.AbsoluteUri;
		return absoluteUri.EndsWith("/", StringComparison.Ordinal)
			? uri
			: new Uri(absoluteUri + "/");
	}
}