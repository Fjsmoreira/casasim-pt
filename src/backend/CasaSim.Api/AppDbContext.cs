using CasaSim.Core.Models;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace CasaSim.Api;

public sealed class AppDbContext : DbContext
{
    public DbSet<Property> Properties => Set<Property>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Property>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => new { p.ExternalId, p.SourceAgency }).IsUnique();
            entity.HasIndex(p => p.City);
            entity.HasIndex(p => p.Price);
            entity.HasIndex(p => p.Type);
            entity.Property(p => p.Price).HasPrecision(18, 2);
            entity.Property(p => p.AreaM2).HasPrecision(12, 2);
            entity.Property(p => p.LandAreaM2).HasPrecision(12, 2);
            entity.Property(p => p.Images).HasColumnType("jsonb");
            entity.Property(p => p.Location).HasColumnType("geometry(Point, 4326)");
        });
    }
}
