using System.Collections.Concurrent;
using System.Threading.Channels;

namespace TourGuideApi.Services;

/// <summary>
/// In-memory, single-consumer queue for localization audio generation.
/// Ensures a localization is not enqueued more than once at a time.
/// </summary>
public sealed class InMemoryAudioGenerationQueue : IAudioGenerationQueue
{
    private readonly Channel<int> _channel;
    private readonly ConcurrentDictionary<int, byte> _enqueued = new();

    public InMemoryAudioGenerationQueue()
    {
        _channel = Channel.CreateUnbounded<int>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
    }

    public bool TryEnqueue(int localizationId)
    {
        if (localizationId <= 0)
        {
            return false;
        }

        if (!_enqueued.TryAdd(localizationId, 0))
        {
            return false;
        }

        // Unbounded channel should accept writes unless completed.
        if (_channel.Writer.TryWrite(localizationId))
        {
            return true;
        }

        _enqueued.TryRemove(localizationId, out _);
        return false;
    }

    public ValueTask<int> DequeueAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAsync(cancellationToken);

    public void MarkCompleted(int localizationId)
        => _enqueued.TryRemove(localizationId, out _);
}
