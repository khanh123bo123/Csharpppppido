using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TouristGuideAppXF.Models;

namespace TouristGuideAppXF.Services
{
    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private string _baseUrl;

        public ApiService()
        {
            _httpClient = new HttpClient();
            // TODO: dieu chinh base URL phu hop voi moi truong test
            // Android emulator: http://10.0.2.2:5000
            // Thiet bi that: http://<IP may tinh>:5000
            _baseUrl = "http://10.0.2.2:5000";
            _httpClient.BaseAddress = new Uri(_baseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<Location> GetLocationByQrCode(string qrCode)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/locations/by-qr?code={qrCode}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<Location>(json);
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                return null;
            }
        }

        public async Task<List<Location>> GetNearbyLocations(double lat, double lng, double radius)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/locations/nearby?lat={lat}&lng={lng}&radius={radius}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<Location>>(json);
                }

                return new List<Location>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                return new List<Location>();
            }
        }
    }
}
