namespace TouristGuideApp;

using TouristGuideApp.Services;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiMaps()
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

		builder.Services.AddHttpClient<IApiService, ApiService>(client =>
		{
			client.BaseAddress = new Uri("https://localhost:5090/");
		});

		// Register Pages
		builder.Services.AddSingleton<MainPage>();

		return builder.Build();
	}
}
