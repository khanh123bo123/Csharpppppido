using System;
using Foundation;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Hosting;

namespace TouristGuideApp;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp()
	{
		// CATCH ALL GLOBAL UNHANDLED EXCEPTIONS
		AppDomain.CurrentDomain.UnhandledException += (s, e) => {
			System.Diagnostics.Debug.WriteLine($"[GLOBAL UNHANDLED EXCEPTION]: {e.ExceptionObject}");
		};
		TaskScheduler.UnobservedTaskException += (s, e) => {
			System.Diagnostics.Debug.WriteLine($"[GLOBAL UNOBSERVED TASK EXCEPTION]: {e.Exception}");
			e.SetObserved();
		};

		try
		{
			return MauiProgram.CreateMauiApp();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[AppDelegate.CreateMauiApp FAIL]: {ex}");
			var builder = MauiApp.CreateBuilder();
			builder.UseMauiApp<FallbackFailApp>();
			return builder.Build();
		}
	}

	class FallbackFailApp : Application
	{
		protected override Window CreateWindow(IActivationState? activationState)
		{
			return new Window(new ContentPage {
				Content = new Label {
					Text = "App Init Failed. See Debug Console.",
					HorizontalOptions = LayoutOptions.Center,
					VerticalOptions = LayoutOptions.Center
				}
			});
		}
	}
}
