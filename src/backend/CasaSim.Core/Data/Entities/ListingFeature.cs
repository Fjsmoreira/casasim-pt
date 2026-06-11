namespace CasaSim.Core.Data.Entities;

public sealed class ListingFeature
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? Unit { get; set; }
    public ListingFeatureType Type { get; set; } = ListingFeatureType.Other;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset FirstSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum ListingFeatureType
{
    Interior = 0,
    Exterior,
    Building,
    Location,
    Amenity,
    Energy,
    Other
}
