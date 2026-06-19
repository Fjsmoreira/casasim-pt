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
    public DbSet<ScraperSource> ScraperSources => Set<ScraperSource>();
    public DbSet<ScrapeListingChange> ScrapeListingChanges => Set<ScrapeListingChange>();

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
        ConfigureScraperSource(modelBuilder.Entity<ScraperSource>());
        ConfigureScrapeListingChange(modelBuilder.Entity<ScrapeListingChange>());
    }

    private static void ConfigureAgency(EntityTypeBuilder<Agency> entity)
    {
        entity.ToTable("agency");
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

        // Seed agencies
        var now = new DateTimeOffset(2026, 6, 11, 0, 0, 0, TimeSpan.Zero);
        entity.HasData(
            new Agency
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000001"),
                Name = "Remax Pombal",
                Slug = "remax-pombal",
                WebsiteUrl = "https://www.remax.pt",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new Agency
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000002"),
                Name = "Century21 Pombal",
                Slug = "century21-pombal",
                WebsiteUrl = "https://www.century21.pt",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new Agency
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000003"),
                Name = "ERA Pombal",
                Slug = "era-pombal",
                WebsiteUrl = "https://www.era.pt",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            }
        );
    }

    private static void ConfigureListing(EntityTypeBuilder<Listing> entity)
    {
        entity.ToTable("listing");
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
        entity.ToTable("listing_image");
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
        entity.ToTable("listing_feature");
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
        entity.ToTable("location");
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
        entity.ToTable("scrape_log");
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

    private static void ConfigureScraperSource(EntityTypeBuilder<ScraperSource> entity)
    {
        entity.ToTable("scraper_source");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Name)
              .HasMaxLength(255)
              .IsRequired();

        entity.Property(e => e.ScraperKey)
              .HasMaxLength(100)
              .IsRequired();

        entity.Property(e => e.AgencySlug)
              .HasMaxLength(255)
              .IsRequired();

        entity.Property(e => e.SourceUrl)
              .HasMaxLength(2048);

        entity.Property(e => e.TargetDescription)
              .HasMaxLength(1000);

        entity.HasOne(e => e.Agency)
              .WithMany()
              .HasForeignKey(e => e.AgencyId)
              .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(e => e.ScraperKey).IsUnique();
        entity.HasIndex(e => e.Enabled);

        var now = new DateTimeOffset(2026, 6, 11, 0, 0, 0, TimeSpan.Zero);
        entity.HasData(
            new ScraperSource
            {
                Id = Guid.Parse("b1000000-0000-0000-0000-000000000001"),
                AgencyId = Guid.Parse("a1000000-0000-0000-0000-000000000001"),
                Name = "Remax Pombal",
                ScraperKey = "Remax",
                AgencySlug = "remax-pombal",
                SourceUrl = "https://www.remax.pt",
                TargetDescription = "Remax listings for Pombal",
                Enabled = true,
                Interval = TimeSpan.FromMinutes(1),
                CreatedAt = now,
                UpdatedAt = now,
            },
            new ScraperSource
            {
                Id = Guid.Parse("b1000000-0000-0000-0000-000000000002"),
                AgencyId = Guid.Parse("a1000000-0000-0000-0000-000000000002"),
                Name = "Century21 Pombal",
                ScraperKey = "Century21",
                AgencySlug = "century21-pombal",
                SourceUrl = "https://www.century21.pt",
                TargetDescription = "Century21 sale and rent listings for Pombal",
                Enabled = true,
                Interval = TimeSpan.FromMinutes(1),
                CreatedAt = now,
                UpdatedAt = now,
            },
            new ScraperSource
            {
                Id = Guid.Parse("b1000000-0000-0000-0000-000000000003"),
                AgencyId = Guid.Parse("a1000000-0000-0000-0000-000000000003"),
                Name = "ERA Pombal",
                ScraperKey = "ERA",
                AgencySlug = "era-pombal",
                SourceUrl = "https://www.era.pt/imoveis/agencia/pombal",
                TargetDescription = "ERA agency listings for Pombal",
                Enabled = true,
                Interval = TimeSpan.FromMinutes(1),
                CreatedAt = now,
                UpdatedAt = now,
            },
            // ── New scrapers (2026-06-19) ────────────────────────
            new ScraperSource
            {
                Id = Guid.Parse("b1000000-0000-0000-0000-000000000004"),
                Name = "Valorfin Imóveis",
                ScraperKey = "Valorfin Imóveis",
                AgencySlug = "valorfin-imoveis",
                SourceUrl = "https://valorfinimoveis.pt",
                TargetDescription = "CRM360 platform — Valorfin Imóveis",
                Enabled = true,
                Interval = TimeSpan.FromHours(6),
                CreatedAt = now,
                UpdatedAt = now,
            },
            new ScraperSource
            {
                Id = Guid.Parse("b1000000-0000-0000-0000-000000000005"),
                Name = "Argilipe",
                ScraperKey = "Argilipe",
                AgencySlug = "argilipe",
                SourceUrl = "https://www.argilipe.pt",
                TargetDescription = "CRM360 platform — Argilipe Imobiliária",
                Enabled = true,
                Interval = TimeSpan.FromHours(6),
                CreatedAt = now,
                UpdatedAt = now,
            },
            new ScraperSource
            {
                Id = Guid.Parse("b1000000-0000-0000-0000-000000000006"),
                Name = "ImoPombal",
                ScraperKey = "ImoPombal",
                AgencySlug = "imopombal",
                SourceUrl = "https://www.imopombal.pt",
                TargetDescription = "eGO platform — ImoPombal",
                Enabled = true,
                Interval = TimeSpan.FromHours(6),
                CreatedAt = now,
                UpdatedAt = now,
            },
            new ScraperSource
            {
                Id = Guid.Parse("b1000000-0000-0000-0000-000000000007"),
                Name = "LionsCastles",
                ScraperKey = "LionsCastles",
                AgencySlug = "lionscastles",
                SourceUrl = "https://www.lionscastles.pt",
                TargetDescription = "eGO platform — LionsCastles",
                Enabled = true,
                Interval = TimeSpan.FromHours(6),
                CreatedAt = now,
                UpdatedAt = now,
            },
            new ScraperSource
            {
                Id = Guid.Parse("b1000000-0000-0000-0000-000000000008"),
                Name = "Habifit",
                ScraperKey = "Habifit",
                AgencySlug = "habifit",
                SourceUrl = "https://www.habifit.pt",
                TargetDescription = "eGO platform — Habifit",
                Enabled = true,
                Interval = TimeSpan.FromHours(6),
                CreatedAt = now,
                UpdatedAt = now,
            },
            new ScraperSource
            {
                Id = Guid.Parse("b1000000-0000-0000-0000-000000000009"),
                Name = "Cosy Imobiliária",
                ScraperKey = "Cosy Imobiliária",
                AgencySlug = "cosy-imobiliaria",
                SourceUrl = "https://www.cosyimobiliaria.pt",
                TargetDescription = "eGO platform — Cosy Imobiliária",
                Enabled = true,
                Interval = TimeSpan.FromHours(6),
                CreatedAt = now,
                UpdatedAt = now,
            },
            new ScraperSource
            {
                Id = Guid.Parse("b1000000-0000-0000-0000-000000000010"),
                Name = "Moderno Imóveis",
                ScraperKey = "Moderno Imóveis",
                AgencySlug = "moderno-imoveis",
                SourceUrl = "https://www.modernoimoveis.pt",
                TargetDescription = "WordPress — Moderno Imóveis",
                Enabled = true,
                Interval = TimeSpan.FromHours(6),
                CreatedAt = now,
                UpdatedAt = now,
            },
            new ScraperSource
            {
                Id = Guid.Parse("b1000000-0000-0000-0000-000000000011"),
                Name = "Neves & Terlouw",
                ScraperKey = "Neves & Terlouw",
                AgencySlug = "neves-terlouw",
                SourceUrl = "https://www.nevesterlouw.com",
                TargetDescription = "Custom site — Neves & Terlouw",
                Enabled = true,
                Interval = TimeSpan.FromHours(6),
                CreatedAt = now,
                UpdatedAt = now,
            },
            new ScraperSource
            {
                Id = Guid.Parse("b1000000-0000-0000-0000-000000000012"),
                Name = "Veigas",
                ScraperKey = "Veigas",
                AgencySlug = "veigas",
                SourceUrl = "https://www.veigas.eu",
                TargetDescription = "Next.js — Veigas Imobiliária",
                Enabled = true,
                Interval = TimeSpan.FromHours(6),
                CreatedAt = now,
                UpdatedAt = now,
            },
            new ScraperSource
            {
                Id = Guid.Parse("b1000000-0000-0000-0000-000000000013"),
                Name = "Zome",
                ScraperKey = "Zome",
                AgencySlug = "zome",
                SourceUrl = "https://www.zome.pt/pt/leiria-h40157/imoveis",
                TargetDescription = "Nuxt/Vue — Zome Leiria district",
                Enabled = true,
                Interval = TimeSpan.FromHours(6),
                CreatedAt = now,
                UpdatedAt = now,
            }
        );
    }

    private static void ConfigureScrapeListingChange(EntityTypeBuilder<ScrapeListingChange> entity)
    {
        entity.ToTable("scrape_listing_change");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Action)
              .HasConversion<string>()
              .HasMaxLength(50);

        entity.Property(e => e.AgencySlug)
              .HasMaxLength(255)
              .IsRequired();

        entity.Property(e => e.ExternalId)
              .HasMaxLength(255)
              .IsRequired();

        entity.Property(e => e.Title)
              .HasMaxLength(500);

        entity.Property(e => e.SourceUrl)
              .HasMaxLength(2048);

        entity.Property(e => e.ChangeSummaryJson)
              .HasColumnType("jsonb");

        entity.HasOne(e => e.ScrapeLog)
              .WithMany(e => e.ListingChanges)
              .HasForeignKey(e => e.ScrapeLogId)
              .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Listing)
              .WithMany()
              .HasForeignKey(e => e.ListingId)
              .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(e => e.ScrapeLogId);
        entity.HasIndex(e => e.Action);
        entity.HasIndex(e => e.CreatedAt);
    }
}
