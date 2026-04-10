using System;
using Microsoft.Maui.Controls;
using TouristGuideApp.Models;
using TouristGuideApp.Services;

namespace TouristGuideApp.Views
{
    [QueryProperty(nameof(POIItem), "POI")]
    public partial class POIDetailsPage : ContentPage
    {
        private POI _poi = null!;
        private readonly IGeofenceService _geofenceService;

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

        public POIDetailsPage(IGeofenceService geofenceService)
        {
            InitializeComponent();
            _geofenceService = geofenceService;
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
                // Fallback image or hide
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
