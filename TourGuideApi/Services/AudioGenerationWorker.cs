using Microsoft.EntityFrameworkCore;
using TourGuideApi.Data;

namespace TourGuideApi.Services;

/// <summary>
/// Background worker that generates audio sequentially to avoid concurrent edge-tts failures.
/// </summary>
public sealed class AudioGenerationWorker : BackgroundService
{
    private readonly IAudioGenerationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AudioGenerationWorker> _logger;

    public AudioGenerationWorker(
        IAudioGenerationQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<AudioGenerationWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SeedPendingAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            int localizationId = 0;
            try
            {
                localizationId = await _queue.DequeueAsync(stoppingToken);

                using var scope = _scopeFactory.CreateScope();
                var generator = scope.ServiceProvider.GetRequiredService<LocalizationAudioGenerator>();

                await generator.GenerateAsync(localizationId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audio generation worker failed for localization {LocalizationId}", localizationId);
            }
            finally
            {
                if (localizationId > 0)
                {
                    _queue.MarkCompleted(localizationId);
                }
            }
        }
    }

    private async Task SeedPendingAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var pendingIds = await context.Localizations
                .AsNoTracking()
                .Where(l => l.AudioGenerationStatus == "pending" && l.CachedAudioBase64 == null)
                .Select(l => l.Id)
                .ToListAsync(stoppingToken);

            foreach (var id in pendingIds)
            {
                _queue.TryEnqueue(id);
            }

            if (pendingIds.Count > 0)
            {
                _logger.LogInformation("Seeded {Count} pending audio job(s) on startup.", pendingIds.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to seed pending audio jobs on startup.");
        }
    }
}
