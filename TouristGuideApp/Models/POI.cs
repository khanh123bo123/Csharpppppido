using System;
using SQLite;

namespace TouristGuideApp.Models
{
    public class POI
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Radius { get; set; } // Bán kính tính bằng mét
        public string Description { get; set; } = string.Empty;

        [Ignore] // Không lưu vào database các thuộc tính trạng thái runtime
        public DateTime LastPlayedTime { get; set; } = DateTime.MinValue;

        [Ignore]
        public bool IsCurrentlyPlaying { get; set; } = false;

        [Ignore]
        public bool HasBeenPlayed { get; set; } = false;
    }
}
