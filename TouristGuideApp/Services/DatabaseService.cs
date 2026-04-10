using SQLite;
using TouristGuideApp.Models;

namespace TouristGuideApp.Services
{
    public interface IDatabaseService
    {
        Task Init();
        Task<List<POI>> GetPOIsAsync();
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

            // Không chèn dữ liệu tĩnh ở đây nữa để đảm bảo danh sách sạch
        }

        public async Task<List<POI>> GetPOIsAsync()
        {
            await Init();
            return await _database!.Table<POI>().ToListAsync();
        }

        public async Task<int> SavePOIAsync(POI poi)
        {
            await Init();
            // Kiểm tra xem đã tồn tại POI này chưa (dựa trên tên hoặc tọa độ) để tránh trùng lặp khi sync
            var existing = await _database!.Table<POI>()
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
    }
}
