using Android.Media;
using Microsoft.Maui.ApplicationModel;

namespace TouristGuideApp.Platforms.Android;

internal static class AudioPlayback
{
    public static Task<bool> PlayAsync(string filePath)
    {
        var tcs = new TaskCompletionSource<bool>();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            MediaPlayer? player = null;
            bool isCleanedUp = false;

            try
            {
                player = new MediaPlayer();
                
                if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.Lollipop)
                {
                    player.SetAudioAttributes(new AudioAttributes.Builder()
                        .SetUsage(AudioUsageKind.Media)
                        .SetContentType(AudioContentType.Music)
                        .Build());
                }

                player.Completion += (_, __) =>
                {
                    if (isCleanedUp) return;
                    isCleanedUp = true;
                    Cleanup(player);
                    tcs.TrySetResult(true);
                };

                player.Error += (_, __) =>
                {
                    if (isCleanedUp) return;
                    isCleanedUp = true;
                    Cleanup(player);
                    tcs.TrySetResult(false);
                };

                player.Prepared += (_, __) =>
                {
                    try
                    {
                        if (!isCleanedUp) player.Start();
                    }
                    catch
                    {
                        if (!isCleanedUp)
                        {
                            isCleanedUp = true;
                            Cleanup(player);
                            tcs.TrySetResult(false);
                        }
                    }
                };

                player.SetDataSource(filePath);
                player.PrepareAsync();
            }
            catch
            {
                if (!isCleanedUp)
                {
                    isCleanedUp = true;
                    Cleanup(player);
                    tcs.TrySetResult(false);
                }
            }
        });

        return tcs.Task;
    }

    private static void Cleanup(MediaPlayer? player)
    {
        if (player == null) return;

        try { player.Stop(); } catch { }
        try { player.Reset(); } catch { }
        try { player.Release(); } catch { }
        try { player.Dispose(); } catch { }
    }
}
