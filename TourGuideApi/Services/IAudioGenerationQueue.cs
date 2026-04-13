using System.Threading.Channels;

namespace TourGuideApi.Services;

public interface IAudioGenerationQueue
{
    bool TryEnqueue(int localizationId);
    ValueTask<int> DequeueAsync(CancellationToken cancellationToken);
    void MarkCompleted(int localizationId);
}
