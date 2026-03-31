using Microsoft.EntityFrameworkCore;
using TourGuideApi.Models;

namespace TourGuideApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Location> Locations { get; set; }
}