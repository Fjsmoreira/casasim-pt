using NetTopologySuite.Geometries;

namespace CasaSim.Core.Models;

public sealed class Property
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExternalId { get; set; } = string.Empty;
    public string SourceAgency { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "EUR";
    public PropertyType Type { get; set; }
    public TransactionType Transaction { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; } = "Pombal";
    public string? District { get; set; } = "Leiria";
    public string? PostalCode { get; set; }
    public Point? Location { get; set; }
    public double? AreaM2 { get; set; }
    public double? LandAreaM2 { get; set; }
    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public int? ParkingSpots { get; set; }
    public int? YearBuilt { get; set; }
    public string? EnergyClass { get; set; }
    public List<string> Images { get; set; } = [];
    public string? ListingUrl { get; set; }
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public PropertyStatus Status { get; set; } = PropertyStatus.Active;
}

public enum PropertyType
{
    Apartment,
    House,
    Villa,
    Townhouse,
    Land,
    Commercial,
    Other
}

public enum TransactionType
{
    Sale,
    Rent
}

public enum PropertyStatus
{
    Active,
    Pending,
    Sold,
    Rented,
    Removed
}
