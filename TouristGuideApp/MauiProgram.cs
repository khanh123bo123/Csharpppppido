namespace TouristGuideApp;

using Plugin.Maui.Audio;
using TouristGuideApp.Services;
using ZXing.Net.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
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

		builder.Services.AddSingleton(AudioManager.Current);
		builder.Services.AddHttpClient<IApiService, ApiService>(client =>
		{
			client.BaseAddress = new Uri("https://localhost:5090/");
		});

		return builder.Build();
	}
}
