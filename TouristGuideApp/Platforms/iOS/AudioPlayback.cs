using AVFoundation;
using Foundation;
using Microsoft.Maui.ApplicationModel;

namespace TouristGuideApp.Platforms.iOS;

internal static class AudioPlayback
{
    public static Task<bool> PlayAsync(string filePath)
    {
        var tcs = new TaskCompletionSource<bool>();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var url = NSUrl.FromFilename(filePath);
                var player = AVAudioPlayer.FromUrl(url);
                if (player == null)
                {
                    tcs.TrySetResult(false);
                    return;
                }

                player.FinishedPlaying += (_, __) =>
                {
                    try { player.Dispose(); } catch { }
                    tcs.TrySetResult(true);
                };

                if (!player.Play())
                {
                    try { player.Dispose(); } catch { }
                    tcs.TrySetResult(false);
                }
            }
            catch
            {
                tcs.TrySetResult(false);
            }
        });

        return tcs.Task;
    }
}
