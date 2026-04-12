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
            try
            {
                player = new MediaPlayer();

                player.Completion += (_, __) =>
                {
                    Cleanup(player);
                    tcs.TrySetResult(true);
                };

                player.Error += (_, __) =>
                {
                    Cleanup(player);
                    tcs.TrySetResult(false);
                };

                player.Prepared += (_, __) =>
                {
                    try
                    {
                        player.Start();
                    }
                    catch
                    {
                        Cleanup(player);
                        tcs.TrySetResult(false);
                    }
                };

                player.SetDataSource(filePath);
                player.PrepareAsync();
            }
            catch
            {
                Cleanup(player);
                tcs.TrySetResult(false);
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
