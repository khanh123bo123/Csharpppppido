namespace TouristGuideApp.Services;

public interface IApiService
{
    Task<IReadOnlyList<TouristGuideApp.Models.Location>> GetLocationsAsync(CancellationToken cancellationToken = default);
    Task<TouristGuideApp.Models.Location> GetLocationByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> CreateLocationAsync(TouristGuideApp.Models.Location location, CancellationToken cancellationToken = default);
    Task<bool> UpdateLocationAsync(TouristGuideApp.Models.Location location, CancellationToken cancellationToken = default);
    Task<bool> DeleteLocationAsync(int id, CancellationToken cancellationToken = default);
}
