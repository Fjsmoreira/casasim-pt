using CasaSim.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CasaSim.Api;

public sealed class AppDbContext : DbContext
{
    public DbSet<Agency> Agencies => Set<Agency>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<ListingImage> ListingImages => Set<ListingImage>();
    public DbSet<ListingFeature> ListingFeatures => Set<ListingFeature>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<ScrapeLog> ScrapeLogs => Set<ScrapeLog>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");

        ConfigureAgency(modelBuilder.Entity<Agency>());
        ConfigureListing(modelBuilder.Entity<Listing>());
        ConfigureListingImage(modelBuilder.Entity<ListingImage>());
        ConfigureListingFeature(modelBuilder.Entity<ListingFeature>());
        ConfigureLocation(modelBuilder.Entity<Location>());
        ConfigureScrapeLog(modelBuilder.Entity<ScrapeLog>());
    }

    private static void ConfigureAgency(EntityTypeBuilder<Agency> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Name)
              .HasMaxLength(255)
              .IsRequired();

        entity.Property(e => e.Slug)
              .HasMaxLength(255)
              .IsRequired();

        entity.Property(e => e.WebsiteUrl)
              .HasMaxLength(2048);

        entity.Property(e => e.ContactEmail)
              .HasMaxLength(255);

        entity.Property(e => e.ContactPhone)
              .HasMaxLength(50);

        entity.HasIndex(e => e.Slug).IsUnique();
    }

    private static void ConfigureListing(EntityTypeBuilder<Listing> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.ExternalId)
              .HasMaxLength(255)
              .IsRequired();

        entity.Property(e => e.SourceUrl)
              .HasMaxLength(2048)
              .IsRequired();

        entity.Property(e => e.CanonicalUrl)
              .HasMaxLength(2048);

        entity.Property(e => e.Title)
              .HasMaxLength(500)
              .IsRequired();

        entity.Property(e => e.Currency)
              .HasMaxLength(10)
              .IsRequired();

        entity.Property(e => e.Price)
              .HasPrecision(18, 2);

        entity.Property(e => e.AreaM2)
              .HasPrecision(12, 2);

        entity.Property(e => e.LandAreaM2)
              .HasPrecision(12, 2);

        entity.Property(e => e.EnergyClass)
              .HasMaxLength(10);

        entity.Property(e => e.City)
              .HasMaxLength(255);

        entity.Property(e => e.PropertyType)
              .HasConversion<string>()
              .HasMaxLength(50);

        entity.Property(e => e.Status)
              .HasConversion<string>()
              .HasMaxLength(50);

        entity.Property(e => e.PriceType)
              .HasConversion<string>()
              .HasMaxLength(50);

        // Relationships
        entity.HasOne(e => e.Agency)
              .WithMany(a => a.Listings)
              .HasForeignKey(e => e.AgencyId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Location)
              .WithMany(l => l.Listings)
              .HasForeignKey(e => e.LocationId)
              .OnDelete(DeleteBehavior.SetNull);

        entity.HasMany(e => e.Images)
              .WithOne(i => i.Listing)
              .HasForeignKey(i => i.ListingId)
              .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(e => e.Features)
              .WithOne(f => f.Listing)
              .HasForeignKey(f => f.ListingId)
              .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        entity.HasIndex(e => e.Status);
        entity.HasIndex(e => e.Price);
        entity.HasIndex(e => e.PropertyType);
        entity.HasIndex(e => e.City);
        entity.HasIndex(e => new { e.AgencyId, e.ExternalId }).IsUnique();
    }

    private static void ConfigureListingImage(EntityTypeBuilder<ListingImage> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Url)
              .HasMaxLength(2048)
              .IsRequired();

        entity.Property(e => e.ThumbnailUrl)
              .HasMaxLength(2048);

        entity.Property(e => e.AltText)
              .HasMaxLength(500);
    }

    private static void ConfigureListingFeature(EntityTypeBuilder<ListingFeature> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Name)
              .HasMaxLength(255)
              .IsRequired();

        entity.Property(e => e.Value)
              .HasMaxLength(500);

        entity.Property(e => e.Unit)
              .HasMaxLength(50);

        entity.Property(e => e.Type)
              .HasConversion<string>()
              .HasMaxLength(50);
    }

    private static void ConfigureLocation(EntityTypeBuilder<Location> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.AddressLine1)
              .HasMaxLength(500);

        entity.Property(e => e.AddressLine2)
              .HasMaxLength(500);

        entity.Property(e => e.Parish)
              .HasMaxLength(255);

        entity.Property(e => e.Municipality)
              .HasMaxLength(255)
              .IsRequired();

        entity.Property(e => e.District)
              .HasMaxLength(255)
              .IsRequired();

        entity.Property(e => e.CountryCode)
              .HasMaxLength(5)
              .IsRequired();

        entity.Property(e => e.PostalCode)
              .HasMaxLength(20);

        entity.Property(e => e.Geohash)
              .HasMaxLength(20);

        entity.Property(e => e.Latitude)
              .HasPrecision(10, 7);

        entity.Property(e => e.Longitude)
              .HasPrecision(10, 7);

        entity.Property(e => e.Coordinate)
              .HasColumnType("geometry(Point, 4326)");

        entity.HasIndex(e => e.Municipality);
        entity.HasIndex(e => e.Coordinate).HasMethod("GIST");
    }

    private static void ConfigureScrapeLog(EntityTypeBuilder<ScrapeLog> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.SourceName)
              .HasMaxLength(255)
              .IsRequired();

        entity.Property(e => e.SourceUrl)
              .HasMaxLength(2048);

        entity.Property(e => e.Status)
              .HasConversion<string>()
              .HasMaxLength(50);

        entity.Property(e => e.ErrorMessage)
              .HasMaxLength(2000);

        entity.HasOne(e => e.Agency)
              .WithMany(a => a.ScrapeLogs)
              .HasForeignKey(e => e.AgencyId)
              .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(e => e.Status);
        entity.HasIndex(e => e.StartedAt);
    }
}
