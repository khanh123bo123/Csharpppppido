namespace TouristGuideApp;

using TouristGuideApp.Services;
using TouristGuideApp.Views;
using Microsoft.Maui.Controls.Hosting;
using ZXing.Net.Maui.Controls;

public static class MauiProgram
{
	private const int DefaultApiPort = 5214;
	private const string ProductionApiBaseUrl = "http://172.20.10.2:5214/";

	public static MauiApp CreateMauiApp()
	{
		try
		{
			var builder = MauiApp.CreateBuilder();
			builder
				.UseMauiApp<App>()
                .UseBarcodeReader()
                .ConfigureFonts(fonts =>
				{
					fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
					fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				});

			// Register Services
			builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
			builder.Services.AddSingleton<IAudioService, AudioService>();
			builder.Services.AddSingleton<IAuthService, AuthService>();
			builder.Services.AddSingleton<ILocationService, LocationService>();
			builder.Services.AddSingleton<IGeofenceService, GeofenceService>();
			builder.Services.AddSingleton<ISyncService, SyncService>();
			builder.Services.AddSingleton<IOfflineMapService, OfflineMapService>();
			builder.Services.AddSingleton<IMapHtmlGenerator, MapHtmlGenerator>();
			builder.Services.AddSingleton<IUpdateService, UpdateService>();

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
			builder.Services.AddSingleton<AppShell>();
			builder.Services.AddSingleton<MainPage>();
			builder.Services.AddSingleton<MapPage>();
			builder.Services.AddSingleton<SettingsPage>();
			builder.Services.AddTransient<POIDetailsPage>();
            builder.Services.AddTransient<ToursPage>();
            builder.Services.AddTransient<QRScanPage>();
            builder.Services.AddTransient<TourMapPage>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<RegisterPage>();

            var app = builder.Build();

            // PRE-FLIGHT CHECK: 
            // Resolve App manually to ensure no DI crashes happen invisibly.
            // If this fails, it's a DI misconfiguration.
            try
            {
                var testApp = app.Services.GetService<App>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FATAL in DI Resolution]: {ex}");
                throw new InvalidOperationException($"DI Registration failed: {ex.Message}", ex);
            }

			return app;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"!!!FATAL ERROR during app initialization: {ex}");
			
			// Build a safe fallback app and print the actual Exception to the UI
			var fallbackBuilder = MauiApp.CreateBuilder();
			fallbackBuilder.UseMauiApp<FallbackFailApp>();
			FallbackFailApp.ErrorMessage = ex.ToString();
			return fallbackBuilder.Build();
		}
	}

	class FallbackFailApp : Application
	{
		public static string ErrorMessage { get; set; } = "N/A";
		
		protected override Window CreateWindow(IActivationState? activationState)
		{
			return new Window(new ContentPage {
				Content = new ScrollView {
					Content = new Label {
						Text = "APP INIT FAILED!\n\n" + ErrorMessage,
						TextColor = Colors.Red,
						Margin = 20,
						FontAttributes = FontAttributes.Bold
					}
				}
			});
		}
	}

	private static Uri ResolveApiBaseAddress()
	{
		var customApiBaseUrl = AppPreferences.GetApiBaseUrl();
		if (!string.IsNullOrWhiteSpace(customApiBaseUrl) &&
			Uri.TryCreate(customApiBaseUrl, UriKind.Absolute, out var customUri))
		{
			return EnsureTrailingSlash(customUri);
		}

		#if DEBUG
		#if ANDROID
		var isEmulator = Microsoft.Maui.Devices.DeviceInfo.Current.DeviceType == Microsoft.Maui.Devices.DeviceType.Virtual;
		var host = isEmulator ? "10.0.2.2" : "172.20.10.2";
		return new Uri($"http://{host}:{DefaultApiPort}/");
		#elif IOS
		// Your Mac IP is: 172.20.10.2
		return new Uri($"http://172.20.10.2:{DefaultApiPort}/");
		#else
		return new Uri($"http://localhost:{DefaultApiPort}/");
		#endif
		#else
		if (!Uri.TryCreate(ProductionApiBaseUrl, UriKind.Absolute, out var productionUri))
		{
			throw new InvalidOperationException("Production API base URL is not configured. Set ProductionApiBaseUrl in MauiProgram.cs.");
		}

		return EnsureTrailingSlash(productionUri);
		#endif
	}

	private static bool IsPrivateHost(string host)
	{
		if (string.IsNullOrWhiteSpace(host))
		{
			return true;
		}

		var h = host.Trim().ToLowerInvariant();
		if (h is "localhost" or "127.0.0.1" or "::1")
		{
			return true;
		}

		if (!System.Net.IPAddress.TryParse(h, out var ip))
		{
			return false;
		}

		var bytes = ip.GetAddressBytes();
		if (bytes.Length != 4) return false;
		return bytes[0] == 10
			|| (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
			|| (bytes[0] == 192 && bytes[1] == 168);
	}

	private static Uri EnsureTrailingSlash(Uri uri)
	{
		var absoluteUri = uri.AbsoluteUri;
		return absoluteUri.EndsWith("/", StringComparison.Ordinal)
			? uri
			: new Uri(absoluteUri + "/");
	}
}