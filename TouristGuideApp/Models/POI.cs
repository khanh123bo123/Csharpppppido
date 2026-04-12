using System;
using SQLite;

namespace TouristGuideApp.Models
{
    public class POI
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // Backend Location ID (used to fetch localization + audio from API)
        public int ServerLocationId { get; set; }

        // QR code payload (matches TourGuideApi Location.QrCodeData)
        public string? QrCodeData { get; set; }

        // Thông tin cơ bản
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? ImageUrl { get; set; }

        // Tọa độ và bán kính (Tài liệu yêu cầu 30m cho khu vực đô thị dày đặc)
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Radius { get; set; } = 30;

        // Đa ngôn ngữ (VN, EN, CN, JP, KR)
        public string LanguageCode { get; set; } = "vi-VN";

        // Âm thanh thuyết minh (4-Tier Hybrid)
        public string? AudioUrl { get; set; }

        [Ignore]
        public DateTime LastPlayedTime { get; set; } = DateTime.MinValue;

        [Ignore]
        public bool IsCurrentlyPlaying { get; set; } = false;

        [Ignore]
        public bool HasBeenPlayed { get; set; } = false;

        [Ignore]
        public double DistanceToUser { get; set; }

        [Ignore]
        public string DistanceText => DistanceToUser < 1000
            ? $"{(int)DistanceToUser}m"
            : $"{DistanceToUser/1000:F1}km";

        // Màu sắc trạng thái dựa trên phân loại hoặc hoạt động
        [Ignore]
        public Color StatusColor => IsCurrentlyPlaying ? Colors.Orange : (DistanceToUser <= Radius ? Colors.LightGreen : Colors.Transparent);
    }
}
