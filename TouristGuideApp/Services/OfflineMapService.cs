using System;
using System.IO;
using System.Threading.Tasks;

namespace TouristGuideApp.Services
{
    /// <summary>
    /// Offline map tile caching service
    /// Supports Hybrid mode: Online with fallback to cached tiles
    /// </summary>
    public interface IOfflineMapService
    {
        Task InitializeAsync();
        Task<string> GetTileCachePathAsync();
        Task CacheTilesAsync(double minLat, double minLon, double maxLat, double maxLon, int zoomLevel);
        Task<bool> IsTileCachedAsync(string tileUrl);
        Task<byte[]> GetCachedTileAsync(string tileUrl);
        Task SaveCachedTileAsync(string tileUrl, byte[] tileData);
        Task ClearCacheAsync();
    }

    public class OfflineMapService : IOfflineMapService
    {
        private string _cacheDirectory = string.Empty;
        private const string CACHE_DIR_NAME = "MapTiles";

        public async Task InitializeAsync()
        {
            try
            {
                // Create cache directory in app's local data folder
                var localPath = FileSystem.Current.AppDataDirectory;
                _cacheDirectory = Path.Combine(localPath, CACHE_DIR_NAME);

                if (!Directory.Exists(_cacheDirectory))
                {
                    Directory.CreateDirectory(_cacheDirectory);
                }

                System.Diagnostics.Debug.WriteLine($"Map cache initialized at: {_cacheDirectory}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing offline map service: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        public async Task<string> GetTileCachePathAsync()
        {
            if (string.IsNullOrEmpty(_cacheDirectory))
            {
                await InitializeAsync();
            }
            return _cacheDirectory;
        }

        /// <summary>
        /// Pre-cache tiles for a specific region and zoom level
        /// Region defined by lat/lon boundaries
        /// </summary>
        public async Task CacheTilesAsync(double minLat, double minLon, double maxLat, double maxLon, int zoomLevel)
        {
            try
            {
                // Tile coordinates calculation (Web Mercator projection)
                var minTile = LatLonToTile(minLat, minLon, zoomLevel);
                var maxTile = LatLonToTile(maxLat, maxLon, zoomLevel);

                int tileCount = 0;
                for (int x = minTile.Item1; x <= maxTile.Item1; x++)
                {
                    for (int y = minTile.Item2; y <= maxTile.Item2; y++)
                    {
                        // OpenStreetMap tile URL pattern: https://tile.openstreetmap.org/{z}/{x}/{y}.png
                        string tileUrl = $"https://tile.openstreetmap.org/{zoomLevel}/{x}/{y}.png";
                        
                        // Check if already cached
                        if (!await IsTileCachedAsync(tileUrl))
                        {
                            try
                            {
                                using (var client = new HttpClient())
                                {
                                    var response = await client.GetAsync(tileUrl);
                                    if (response.IsSuccessStatusCode)
                                    {
                                        var tileData = await response.Content.ReadAsByteArrayAsync();
                                        await SaveCachedTileAsync(tileUrl, tileData);
                                        tileCount++;
                                    }
                                }
                            }
                            catch
                            {
                                // Skip failed tiles
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Cached {tileCount} map tiles for zoom level {zoomLevel}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error caching tiles: {ex.Message}");
            }
        }

        public async Task<bool> IsTileCachedAsync(string tileUrl)
        {
            try
            {
                var cachePath = GetTileCachePath(tileUrl);
                return await Task.FromResult(File.Exists(cachePath));
            }
            catch
            {
                return false;
            }
        }

        public async Task<byte[]> GetCachedTileAsync(string tileUrl)
        {
            try
            {
                var cachePath = GetTileCachePath(tileUrl);
                if (File.Exists(cachePath))
                {
                    return await Task.FromResult(File.ReadAllBytes(cachePath));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading cached tile: {ex.Message}");
            }

            return Array.Empty<byte>();
        }

        public async Task SaveCachedTileAsync(string tileUrl, byte[] tileData)
        {
            try
            {
                var cachePath = GetTileCachePath(tileUrl);
                var directory = Path.GetDirectoryName(cachePath);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllBytesAsync(cachePath, tileData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving cached tile: {ex.Message}");
            }
        }

        public async Task ClearCacheAsync()
        {
            try
            {
                if (Directory.Exists(_cacheDirectory))
                {
                    Directory.Delete(_cacheDirectory, true);
                    Directory.CreateDirectory(_cacheDirectory);
                }
                System.Diagnostics.Debug.WriteLine("Map cache cleared");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing cache: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Convert latitude/longitude to tile coordinates (Web Mercator)
        /// </summary>
        private (int, int) LatLonToTile(double lat, double lon, int zoom)
        {
            int n = 1 << zoom; // 2^zoom
            int x = (int)((lon + 180.0) / 360.0 * n);
            int y = (int)((1.0 - Math.Log(Math.Tan(lat * Math.PI / 180.0) + 1.0 / Math.Cos(lat * Math.PI / 180.0)) / Math.PI) / 2.0 * n);
            return (x, y);
        }

        /// <summary>
        /// Generate cache file path from tile URL
        /// URL: https://tile.openstreetmap.org/z/x/y.png
        /// Path: cache/z/x/y.png
        /// </summary>
        private string GetTileCachePath(string tileUrl)
        {
            // Extract z/x/y from URL
            var uri = new Uri(tileUrl);
            var parts = uri.AbsolutePath.Trim('/').Split('/');
            
            if (parts.Length >= 3)
            {
                string z = parts[parts.Length - 3];
                string x = parts[parts.Length - 2];
                string y = parts[parts.Length - 1];

                return Path.Combine(_cacheDirectory, z, x, $"{y}.png");
            }

            return Path.Combine(_cacheDirectory, tileUrl.GetHashCode().ToString());
        }
    }
}
