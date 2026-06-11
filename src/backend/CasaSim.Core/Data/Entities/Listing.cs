namespace CasaSim.Core.Data.Entities;

public sealed class Listing
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AgencyId { get; set; }
    public Agency? Agency { get; set; }

    public Guid? LocationId { get; set; }
    public Location? Location { get; set; }

    public string ExternalId { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string? CanonicalUrl { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string? City { get; set; }

    public ListingPropertyType PropertyType { get; set; } = ListingPropertyType.Unknown;
    public ListingStatus Status { get; set; } = ListingStatus.Active;
    public ListingPriceType PriceType { get; set; } = ListingPriceType.Sale;

    public decimal? Price { get; set; }
    public string Currency { get; set; } = "EUR";
    public decimal? AreaM2 { get; set; }
    public decimal? LandAreaM2 { get; set; }
    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public int? ParkingSpaces { get; set; }
    public int? YearBuilt { get; set; }
    public string? EnergyClass { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset FirstSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }

    public ICollection<ListingImage> Images { get; set; } = new List<ListingImage>();
    public ICollection<ListingFeature> Features { get; set; } = new List<ListingFeature>();
}

public enum ListingPropertyType
{
    Unknown = 0,
    Apartment,
    House,
    Villa,
    Townhouse,
    Farm,
    Land,
    Commercial,
    Garage,
    Other
}

public enum ListingStatus
{
    Active = 0,
    Reserved,
    Pending,
    Sold,
    Rented,
    Removed,
    Archived
}

public enum ListingPriceType
{
    Sale = 0,
    Rent,
    PriceOnRequest,
    Auction,
    Unknown
}
