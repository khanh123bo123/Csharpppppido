using AVFoundation;
using Foundation;
using Microsoft.Maui.ApplicationModel;

namespace TouristGuideApp.Platforms.iOS;

internal static class AudioPlayback
{
    public static async Task<bool> PlayAsync(string filePath)
    {
        var tcs = new TaskCompletionSource<bool>();
        
        // Safety timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        cts.Token.Register(() => tcs.TrySetResult(false));

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

                EventHandler<AVStatusEventArgs> finishedHandler = null;
                finishedHandler = (_, __) =>
                {
                    player.FinishedPlaying -= finishedHandler;
                    try { player.Dispose(); } catch { }
                    tcs.TrySetResult(true);
                };

                player.FinishedPlaying += finishedHandler;

                if (!player.Play())
                {
                    player.FinishedPlaying -= finishedHandler;
                    try { player.Dispose(); } catch { }
                    tcs.TrySetResult(false);
                }
            }
            catch
            {
                tcs.TrySetResult(false);
            }
        });

        return await tcs.Task;
    }
}
