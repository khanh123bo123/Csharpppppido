using TouristGuideApp.Models;

namespace TouristGuideApp.Services;

public interface IApiService
{
    Task<IReadOnlyList<Location>> GetLocationsAsync(CancellationToken cancellationToken = default);
    Task<Location?> GetLocationByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> CreateLocationAsync(Location location, CancellationToken cancellationToken = default);
    Task<bool> UpdateLocationAsync(Location location, CancellationToken cancellationToken = default);
    Task<bool> DeleteLocationAsync(int id, CancellationToken cancellationToken = default);
}
