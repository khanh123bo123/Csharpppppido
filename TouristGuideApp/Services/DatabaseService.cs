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

            // Seed data if empty
            var count = await _database.Table<POI>().CountAsync();
            if (count == 0)
            {
                await _database.InsertAllAsync(new List<POI>
                {
                    new POI { Name = "Hồ Hoàn Kiếm", Latitude = 21.0285, Longitude = 105.8522, Radius = 100, Description = "Chào mừng bạn đến với Hồ Hoàn Kiếm, trái tim của thủ đô Hà Nội." },
                    new POI { Name = "Nhà Thờ Lớn", Latitude = 21.0287, Longitude = 105.8490, Radius = 50, Description = "Bạn đang đứng trước Nhà Thờ Lớn Hà Nội, kiến trúc Gothic tuyệt đẹp." }
                });
            }
        }

        public async Task<List<POI>> GetPOIsAsync()
        {
            await Init();
            return await _database!.Table<POI>().ToListAsync();
        }

        public async Task<int> SavePOIAsync(POI poi)
        {
            await Init();
            if (poi.Id != 0) return await _database!.UpdateAsync(poi);
            return await _database!.InsertAsync(poi);
        }

        public async Task<int> DeletePOIAsync(POI poi)
        {
            await Init();
            return await _database!.DeleteAsync(poi);
        }
    }
}
