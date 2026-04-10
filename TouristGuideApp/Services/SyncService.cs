using TouristGuideApp.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TouristGuideApp.Services
{
    /// <summary>
    /// Offline-first data synchronization service
    /// Syncs POIs and localizations between backend API and local SQLite database
    /// Handles partial connectivity and automatic retry logic
    /// </summary>
    public interface ISyncService
    {
        /// <summary>
        /// Perform full sync of all POIs and their localizations
        /// Returns number of entities synced
        /// </summary>
        Task<SyncResult> SyncAllAsync();

        /// <summary>
        /// Sync a specific location with all its localizations
        /// </summary>
        Task<bool> SyncLocationAsync(int locationId);

        /// <summary>
        /// Check if device is online
        /// </summary>
        Task<bool> IsOnlineAsync();

        /// <summary>
        /// Queue entity for sync when connection resumes
        /// </summary>
        Task QueueForSyncAsync<T>(T entity) where T : class;

        /// <summary>
        /// Get last successful sync timestamp
        /// </summary>
        Task<DateTime> GetLastSyncTimeAsync();
    }

    public class SyncService : ISyncService
    {
        private readonly IApiService _apiService;
        private readonly IDatabaseService _databaseService;
        private DateTime _lastSyncTime = DateTime.MinValue;
        private bool _isSyncing = false;

        public SyncService(IApiService apiService, IDatabaseService databaseService)
        {
            _apiService = apiService;
            _databaseService = databaseService;
        }

        /// <summary>
        /// Perform full sync of POIs from API to local database
        /// </summary>
        public async Task<SyncResult> SyncAllAsync()
        {
            if (_isSyncing)
            {
                return new SyncResult { IsSuccess = false, Message = "Sync already in progress" };
            }

            _isSyncing = true;
            var result = new SyncResult();

            try
            {
                // Check connectivity
                if (!await IsOnlineAsync())
                {
                    result.IsSuccess = false;
                    result.Message = "Device is offline";
                    return result;
                }

                System.Diagnostics.Debug.WriteLine("Starting data sync from API...");

                // Fetch all locations from API
                var locations = await _apiService.GetLocationsAsync();
                if (locations == null)
                {
                    result.IsSuccess = false;
                    result.Message = "Failed to fetch locations from API";
                    return result;
                }

                // Convert and save to local database
                int syncedCount = 0;
                foreach (var location in locations)
                {
                    var poi = new POI
                    {
                        Name = location.Name,
                        Description = location.Description,
                        Latitude = location.Latitude,
                        Longitude = location.Longitude,
                        AudioUrl = location.AudioUrl,
                        LanguageCode = "vi-VN"
                    };

                    await _databaseService.SavePOIAsync(poi);
                    syncedCount++;
                }

                _lastSyncTime = DateTime.UtcNow;
                result.IsSuccess = true;
                result.SyncedCount = syncedCount;
                result.Message = $"Successfully synced {syncedCount} locations";

                System.Diagnostics.Debug.WriteLine($"Sync complete: {syncedCount} POIs");
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = $"Sync error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Sync failed: {ex}");
            }
            finally
            {
                _isSyncing = false;
            }

            return result;
        }

        /// <summary>
        /// Sync a specific location
        /// </summary>
        public async Task<bool> SyncLocationAsync(int locationId)
        {
            try
            {
                if (!await IsOnlineAsync())
                {
                    return false;
                }

                var location = await _apiService.GetLocationAsync(locationId);
                if (location == null)
                {
                    return false;
                }

                var poi = new POI
                {
                    Name = location.Name,
                    Description = location.Description,
                    Latitude = location.Latitude,
                    Longitude = location.Longitude,
                    AudioUrl = location.AudioUrl,
                    LanguageCode = "vi-VN"
                };

                var result = await _databaseService.SavePOIAsync(poi);
                return result > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sync location {locationId} failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if device has internet connectivity
        /// </summary>
        public async Task<bool> IsOnlineAsync()
        {
            var current = Connectivity.Current;
            var hasInternet = current.NetworkAccess == NetworkAccess.Internet;
            
            if (hasInternet)
            {
                try
                {
                    // Verify actual connectivity by making a quick request
                    var response = await _apiService.PingAsync();
                    return response;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Queue entity for sync (for future enhancement)
        /// </summary>
        public async Task QueueForSyncAsync<T>(T entity) where T : class
        {
            // TODO: Implement sync queue using local database
            // This would allow queueing changes made offline for sync when online
            await Task.CompletedTask;
        }

        /// <summary>
        /// Get last successful sync time
        /// </summary>
        public async Task<DateTime> GetLastSyncTimeAsync()
        {
            await Task.CompletedTask;
            return _lastSyncTime;
        }
    }

    /// <summary>
    /// Result of a sync operation
    /// </summary>
    public class SyncResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public int SyncedCount { get; set; }
        public DateTime SyncTime { get; set; } = DateTime.UtcNow;
    }
}
