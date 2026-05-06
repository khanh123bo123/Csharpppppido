using ObjCRuntime;
using UIKit;

namespace TouristGuideApp;

public class Program
{
	// This is the main entry point of the application.
	static void Main(string[] args)
	{
		try
		{
			// if you want to use a different Application Delegate class from "AppDelegate"
			// you can specify it here.
			UIApplication.Main(args, null, typeof(AppDelegate));
		}
		catch (Exception ex)
		{
			// CRITICAL: Log the native iOS crash to console before exiting.
			System.Diagnostics.Debug.WriteLine($"[FATAL CRASH in UIApplication.Main]: {ex}");
			throw;
		}
	}
}
