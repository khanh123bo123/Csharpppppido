using System;
using Microsoft.Maui.Controls;
using TouristGuideApp.Models;
using TouristGuideApp.Services;

namespace TouristGuideApp.Views
{
    [QueryProperty(nameof(POIItem), "POI")]
    [QueryProperty(nameof(PoiId), "poiId")]
    public partial class POIDetailsPage : ContentPage
    {
        private POI _poi = null!;
        private readonly IGeofenceService _geofenceService;
        private readonly IApiService _apiService;

        public POI POIItem
        {
            get => _poi;
            set
            {
                _poi = value;
                OnPropertyChanged();
                LoadPOIDetails();
            }
        }

        private string _poiId = string.Empty;
        public string PoiId
        {
            get => _poiId;
            set
            {
                _poiId = value;
                OnPropertyChanged();
                _ = LoadPoiFromIdAsync(_poiId);
            }
        }

        public POIDetailsPage(IGeofenceService geofenceService, IApiService apiService)
        {
            InitializeComponent();
            _geofenceService = geofenceService;
            _apiService = apiService;
        }

        private async Task LoadPoiFromIdAsync(string idString)
        {
            if (int.TryParse(idString, out int id))
            {
                var location = await _apiService.GetLocationAsync(id);
                if (location != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        POIItem = new POI
                        {
                            Id = location.Id,
                            Name = location.Name ?? "Chưa đặt tên",
                            Description = location.Description ?? "Không có mô tả",
                            Category = string.IsNullOrWhiteSpace(location.Category) ? "Chưa phân loại" : location.Category,
                            Latitude = location.Latitude,
                            Longitude = location.Longitude,
                            Address = location.Address,
                            PhoneNumber = location.PhoneNumber,
                            ImageUrl = location.ImageUrl,
                            AudioUrl = location.AudioUrl
                        };
                    });
                }
            }
        }

        private void LoadPOIDetails()
        {
            if (_poi == null) return;

            lblName.Text = _poi.Name;
            lblCategory.Text = _poi.Category;
            lblDistance.Text = $"Cách bạn {_poi.DistanceText}";
            lblDescription.Text = _poi.Description;

            lblAddress.Text = string.IsNullOrWhiteSpace(_poi.Address) ? "Chưa cập nhật địa chỉ" : _poi.Address;
            lblPhone.Text = string.IsNullOrWhiteSpace(_poi.PhoneNumber) ? "Chưa cập nhật SĐT" : _poi.PhoneNumber;

            if (!string.IsNullOrWhiteSpace(_poi.ImageUrl))
            {
                imgLocation.IsVisible = true;
                imgLocation.Source = ImageSource.FromUri(new Uri(_poi.ImageUrl));
            }
            else
            {
                imgLocation.IsVisible = false;
            }
        }

        private async void OnListenClicked(object sender, EventArgs e)
        {
            if (_poi != null)
            {
                await _geofenceService.PlaySpeechAsync(_poi, ignoreCooldown: true);
            }
        }
    }
}
