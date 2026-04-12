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
    public DbSet<Localization> Localizations { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Tour> Tours { get; set; }
    public DbSet<TourLocation> TourLocations { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Localization - Location relationship
        modelBuilder.Entity<Localization>()
            .HasOne(l => l.Location)
            .WithMany()
            .HasForeignKey(l => l.LocationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Create unique index for language per location
        modelBuilder.Entity<Localization>()
            .HasIndex(l => new { l.LocationId, l.LanguageCode })
            .IsUnique()
            .HasDatabaseName("IX_Localization_LocationId_LanguageCode");

        // Seed default admin user
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                Email = "admin@tourguidequan4.com",
                PasswordHash = "$2a$11$LhyhKjdG1es/L2a4sk/Ezeeb.Rq8B.YlwPo7ji2HE92V0HD8ipiK6", // AdminPassword123!
                FullName = "Administrator",
                Role = "Admin",
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}