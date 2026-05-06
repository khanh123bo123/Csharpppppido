using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace TourGuideApi.Services;

public interface IOnlineTracker
{
    void MarkAsOnline(string deviceId);
    int GetOnlineCount();
}

public class OnlineTracker : IOnlineTracker
{
    private readonly IMemoryCache _cache;
    private const string CacheKey = "OnlineDevicesMap";
    private static readonly TimeSpan OnlineThreshold = TimeSpan.FromMinutes(5);

    public OnlineTracker(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void MarkAsOnline(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return;

        // Use a thread-safe dictionary to track last seen times
        var onlineDevices = _cache.GetOrCreate(CacheKey, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(1);
            return new ConcurrentDictionary<string, DateTime>();
        })!;

        onlineDevices[deviceId] = DateTime.UtcNow;
    }

    public int GetOnlineCount()
    {
        if (!_cache.TryGetValue(CacheKey, out ConcurrentDictionary<string, DateTime>? onlineDevices) || onlineDevices == null)
        {
            return 0;
        }

        var threshold = DateTime.UtcNow.Subtract(OnlineThreshold);
        
        // Remove stale devices to keep the map clean
        foreach (var key in onlineDevices.Keys)
        {
            if (onlineDevices.TryGetValue(key, out var lastSeen) && lastSeen < threshold.AddMinutes(-30))
            {
                onlineDevices.TryRemove(key, out _);
            }
        }

        return onlineDevices.Values.Count(v => v > threshold);
    }
}
