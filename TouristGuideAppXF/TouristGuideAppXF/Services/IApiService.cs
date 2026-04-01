using System.Collections.Generic;
using System.Threading.Tasks;
using TouristGuideAppXF.Models;

namespace TouristGuideAppXF.Services
{
    public interface IApiService
    {
        Task<Location> GetLocationByQrCode(string qrCode);

        Task<List<Location>> GetNearbyLocations(double lat, double lng, double radius);
    }
}
