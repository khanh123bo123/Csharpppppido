using SQLite;
using TouristGuideApp.Models;
using System.Linq;

namespace TouristGuideApp.Services
{
    public interface IDatabaseService
    {
        Task Init();
        Task<List<POI>> GetPOIsAsync();
        Task<POI?> GetPoiByServerLocationIdAsync(int serverLocationId);
        Task<POI?> GetPoiByQrCodeDataAsync(string qrCodeData);
        Task<int> SavePOIAsync(POI poi);
        Task<int> DeletePOIAsync(POI poi);
        Task ClearAllPOIsAsync();
    }

    public class DatabaseService : IDatabaseService
    {
        SQLiteAsyncConnection? _database;

        public async Task Init()
        {
            if (_database is not null) return;

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "TouristGuide.db3");
            _database = new SQLiteAsyncConnection(dbPath);
            await _database.CreateTableAsync<POI>();

            // Lightweight schema migration for existing installations
            await EnsurePoiSchemaAsync(_database);

            // Không chèn dữ liệu tĩnh ở đây nữa để đảm bảo danh sách sạch
        }

        public async Task<List<POI>> GetPOIsAsync()
        {
            await Init();
            return await _database!.Table<POI>().ToListAsync();
        }

        public async Task<POI?> GetPoiByServerLocationIdAsync(int serverLocationId)
        {
            await Init();
            if (serverLocationId <= 0) return null;

            return await _database!.Table<POI>()
                .Where(p => p.ServerLocationId == serverLocationId)
                .FirstOrDefaultAsync();
        }

        public async Task<POI?> GetPoiByQrCodeDataAsync(string qrCodeData)
        {
            await Init();
            if (string.IsNullOrWhiteSpace(qrCodeData)) return null;

            var trimmed = qrCodeData.Trim();
            return await _database!.Table<POI>()
                .Where(p => p.QrCodeData == trimmed)
                .FirstOrDefaultAsync();
        }

        public async Task<int> SavePOIAsync(POI poi)
        {
            await Init();
            // Kiểm tra xem đã tồn tại POI này chưa (dựa trên tên hoặc tọa độ) để tránh trùng lặp khi sync
            POI? existing = null;

            // Prefer stable backend ID matching when available
            if (poi.ServerLocationId > 0)
            {
                existing = await _database!.Table<POI>()
                    .Where(p => p.ServerLocationId == poi.ServerLocationId)
                    .FirstOrDefaultAsync();
            }

            // Backward compatibility: fall back to name matching
            existing ??= await _database!.Table<POI>()
                .Where(p => p.Name == poi.Name)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                poi.Id = existing.Id;
                return await _database!.UpdateAsync(poi);
            }

            return await _database!.InsertAsync(poi);
        }

        public async Task<int> DeletePOIAsync(POI poi)
        {
            await Init();
            return await _database!.DeleteAsync(poi);
        }

        public async Task ClearAllPOIsAsync()
        {
            await Init();
            await _database!.DeleteAllAsync<POI>();
        }

        private static async Task EnsurePoiSchemaAsync(SQLiteAsyncConnection database)
        {
            try
            {
                // Ensure table exists first
                var columns = await database.GetTableInfoAsync(nameof(POI));

                var hasServerLocationId = columns.Any(c => string.Equals(c.Name, nameof(POI.ServerLocationId), StringComparison.OrdinalIgnoreCase));
                if (!hasServerLocationId)
                {
                    await database.ExecuteAsync("ALTER TABLE POI ADD COLUMN ServerLocationId INTEGER NOT NULL DEFAULT 0");
                }

                var hasQrCodeData = columns.Any(c => string.Equals(c.Name, nameof(POI.QrCodeData), StringComparison.OrdinalIgnoreCase));
                if (!hasQrCodeData)
                {
                    await database.ExecuteAsync("ALTER TABLE POI ADD COLUMN QrCodeData TEXT");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB schema migration (POI) skipped/failed: {ex.Message}");
            }
        }
    }
}
