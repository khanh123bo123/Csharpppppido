using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using TouristGuideApp.Services;
#if ANDROID
using Android.Content;
using Android.OS;
using AndroidX.Core.Content;
using Application = Microsoft.Maui.Controls.Application;
#endif

namespace TouristGuideApp.Services
{
    public class UpdateService : IUpdateService
    {
        private readonly IApiService _apiService;
        private readonly HttpClient _httpClient;

        public UpdateService(IApiService apiService)
        {
            _apiService = apiService;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        }

        public async Task<bool> CheckAndInstallUpdateAsync()
        {
            try
            {
                // URL cố định cho bản cập nhật mới nhất
                string apkUrl = $"{_apiService.BaseAddress}/TourGuide.apk";
                
                string localPath = Path.Combine(FileSystem.CacheDirectory, "update.apk");

                // Tải file APK
                var response = await _httpClient.GetAsync(apkUrl);
                if (!response.IsSuccessStatusCode) return false;

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = File.Create(localPath))
                {
                    await stream.CopyToAsync(fileStream);
                }

                // Cài đặt trên Android
#if ANDROID
                InstallApkAndroid(localPath);
                return true;
#else
                return false;
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update Error: {ex.Message}");
                return false;
            }
        }

#if ANDROID
        private void InstallApkAndroid(string filePath)
        {
            var context = Android.App.Application.Context;
            var file = new Java.IO.File(filePath);
            var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(context, $"{context.PackageName}.fileprovider", file);

            var intent = new Intent(Intent.ActionView);
            intent.AddFlags(ActivityFlags.NewTask);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission);
            intent.SetDataAndType(uri, "application/vnd.android.package-archive");

            context.StartActivity(intent);
        }
#endif
    }
}
