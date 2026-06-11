namespace CasaSim.Core.Data.Entities;

public sealed class Location
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? Parish { get; set; }
    public string Municipality { get; set; } = "Pombal";
    public string District { get; set; } = "Leiria";
    public string CountryCode { get; set; } = "PT";
    public string? PostalCode { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Geohash { get; set; }
    public string? RawAddress { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset FirstSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Listing> Listings { get; set; } = new List<Listing>();
}
