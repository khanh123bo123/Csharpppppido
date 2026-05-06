using System.Threading.Tasks;

namespace TouristGuideApp.Services
{
    public interface IUpdateService
    {
        Task<bool> CheckAndInstallUpdateAsync();
    }
}
