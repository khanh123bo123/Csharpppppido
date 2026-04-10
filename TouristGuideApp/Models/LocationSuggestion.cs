namespace TouristGuideApp.Models
{
    /// <summary>
    /// Mobile representation of location suggestion
    /// </summary>
    public class LocationSuggestion
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? Category { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; }
        public string? ImageData { get; set; } // Base64
        
        // For display
        public string StatusDisplay => Status switch
        {
            "Pending" => "⏳ Chờ duyệt",
            "Approved" => "✅ Đã phê duyệt",
            "Rejected" => "❌ Bị từ chối",
            _ => Status
        };
    }
}
