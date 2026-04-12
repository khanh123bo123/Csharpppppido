using System.Collections.Generic;

namespace TouristGuideWeb.Models;

public class TourDetailsViewModel
{
    public Tour CurrentTour { get; set; } = new Tour();
    public List<TourLocation> AssignedLocations { get; set; } = new List<TourLocation>();
    public List<Location> AvailableLocations { get; set; } = new List<Location>();
}
