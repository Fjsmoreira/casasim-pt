namespace CasaSim.Core.Models;

/// <summary>
/// Intermediate parsed-listing model — produced by an <c>IAgencyScraper</c>
/// <em>before</em> it is mapped to the DB entity (<c>Listing</c>).
///
/// Carries all scraped fields plus source metadata, error information
/// and the original raw data so the upsert pipeline can make mapping
/// decisions without re-fetching.
/// </summary>
public sealed class ParsedListing
{
    /// <summary>Unique identifier within the source agency (e.g., "122591135-5").</summary>
    public string ExternalId { get; init; } = string.Empty;

    /// <summary>Human-readable property title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Free-text description in the source locale (usually PT).</summary>
    public string? Description { get; init; }

    /// <summary>Asking price in the listing currency.</summary>
    public decimal Price { get; init; }

    /// <summary>Three-letter currency code (default: "EUR").</summary>
    public string Currency { get; init; } = "EUR";

    /// <summary>Property category (Apartment, House, Villa, etc.).</summary>
    public PropertyType Type { get; init; }

    /// <summary>Transaction type (Sale or Rent).</summary>
    public TransactionType Transaction { get; init; }

    // ── Location ──────────────────────────────────────────────

    /// <summary>Street address as returned by the source.</summary>
    public string? Address { get; init; }

    /// <summary>City / municipality name.</summary>
    public string? City { get; init; }

    /// <summary>District / region name.</summary>
    public string? District { get; init; }

    /// <summary>Parish / sub-region name.</summary>
    public string? Parish { get; init; }

    /// <summary>Postal code / ZIP.</summary>
    public string? PostalCode { get; init; }

    /// <summary>Latitude (WGS84).</summary>
    public double? Latitude { get; init; }

    /// <summary>Longitude (WGS84).</summary>
    public double? Longitude { get; init; }

    // ── Property details ─────────────────────────────────────

    /// <summary>Usable floor area in m².</summary>
    public double? AreaM2 { get; init; }

    /// <summary>Land / lot area in m².</summary>
    public double? LandAreaM2 { get; init; }

    /// <summary>Number of bedrooms.</summary>
    public int? Bedrooms { get; init; }

    /// <summary>Number of bathrooms / WC.</summary>
    public int? Bathrooms { get; init; }

    /// <summary>Number of parking spaces / garage spots.</summary>
    public int? ParkingSpots { get; init; }

    /// <summary>Year the property was built.</summary>
    public int? YearBuilt { get; init; }

    /// <summary>Energy efficiency class (A+, A, B, C, …).</summary>
    public string? EnergyClass { get; init; }

    // ── Media & tracking ─────────────────────────────────────

    /// <summary>Image URLs as returned by the source (absolute or relative).</summary>
    public List<string> Images { get; init; } = [];

    /// <summary>Direct URL to the listing on the agency's site.</summary>
    public string? ListingUrl { get; init; }

    /// <summary>Timestamp when the agency says the listing entered the market.</summary>
    public DateTime? PublishedAt { get; init; }

    /// <summary>Timestamp when this listing was first discovered in this scrape cycle.</summary>
    public DateTime DiscoveredAt { get; init; } = DateTime.UtcNow;

    /// <summary>Lifecycle status as reported by the source.</summary>
    public PropertyStatus Status { get; init; } = PropertyStatus.Active;
}
