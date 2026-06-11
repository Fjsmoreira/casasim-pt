using CasaSim.Core.Interfaces;
using CasaSim.Core.Models;

namespace CasaSim.Core.Tests;

public sealed class ScrapeResultTests
{
    [Fact]
    public void Default_HasNoErrors_IsSuccess()
    {
        var result = new ScrapeResult();
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public void EmptyListings_IsNotSuccess_HasNoListings()
    {
        var result = new ScrapeResult
        {
            AgencyName = "Remax",
            AgencySlug = "remax-pombal",
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            CompletedAt = DateTime.UtcNow,
            Listings = Array.Empty<ParsedListing>(),
            Errors = Array.Empty<ScrapeError>(),
            TotalFound = 0,
        };

        Assert.True(result.IsSuccess);
        Assert.False(result.HasListings);
        Assert.Equal(0, result.SuccessCount);
        Assert.NotNull(result.Duration);
        Assert.True(result.Duration.Value.TotalMinutes > 4);
    }

    [Fact]
    public void WithListings_AndErrors_HasPartialSuccess()
    {
        var result = new ScrapeResult
        {
            Listings = [new ParsedListing { ExternalId = "abc", Title = "Casa T3" }],
            Errors = [new ScrapeError { AgencyName = "Remax", ExternalId = "def", Message = "Parse failed" }],
        };

        Assert.False(result.IsSuccess);
        Assert.True(result.HasListings);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.ErrorCount);
    }

    [Fact]
    public void Duration_Null_WhenNotCompleted()
    {
        var result = new ScrapeResult
        {
            StartedAt = DateTime.UtcNow,
            CompletedAt = null,
        };

        Assert.Null(result.Duration);
    }

    [Fact]
    public void FullSuccess_HasDuration_SuccessCount_NoErrors()
    {
        var started = new DateTime(2026, 6, 11, 10, 0, 0, DateTimeKind.Utc);
        var completed = new DateTime(2026, 6, 11, 10, 2, 30, DateTimeKind.Utc);

        var result = new ScrapeResult
        {
            AgencyName = "ERA",
            AgencySlug = "era-pombal",
            StartedAt = started,
            CompletedAt = completed,
            Listings = [
                new ParsedListing { ExternalId = "1", Title = "Apartment A" },
                new ParsedListing { ExternalId = "2", Title = "House B" },
                new ParsedListing { ExternalId = "3", Title = "Villa C" },
            ],
            Errors = Array.Empty<ScrapeError>(),
            TotalFound = 10,
        };

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.SuccessCount);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(10, result.TotalFound);
        Assert.True(result.HasListings);
        Assert.Equal(TimeSpan.FromMinutes(2).Add(TimeSpan.FromSeconds(30)), result.Duration);
    }

    [Fact]
    public void ErrorCount_MatchesErrorsList()
    {
        var result = new ScrapeResult
        {
            Errors = new List<ScrapeError>
            {
                new() { AgencyName = "R", ExternalId = "id1", Message = "Timeout" },
                new() { AgencyName = "R", ExternalId = "id2", Message = "Not found" },
                new() { AgencyName = "R", ExternalId = "id3", Message = "Parse error" },
            },
        };

        Assert.Equal(3, result.ErrorCount);
        Assert.False(result.IsSuccess);
    }
}

public sealed class ParsedListingDefaultsTests
{
    [Fact]
    public void Default_ExternalId_IsEmpty()
    {
        var listing = new ParsedListing();
        Assert.Equal(string.Empty, listing.ExternalId);
    }

    [Fact]
    public void Default_Currency_IsEur()
    {
        var listing = new ParsedListing();
        Assert.Equal("EUR", listing.Currency);
    }

    [Fact]
    public void Default_Status_IsActive()
    {
        var listing = new ParsedListing();
        Assert.Equal(PropertyStatus.Active, listing.Status);
    }

    [Fact]
    public void Default_Type_IsApartment()
    {
        var listing = new ParsedListing();
        Assert.Equal(PropertyType.Apartment, listing.Type);
    }

    [Fact]
    public void Default_Transaction_IsSale()
    {
        var listing = new ParsedListing();
        Assert.Equal(TransactionType.Sale, listing.Transaction);
    }

    [Fact]
    public void Populated_Listing_ReturnsValues()
    {
        var listing = new ParsedListing
        {
            ExternalId = "test-123",
            Title = "Beautiful Villa",
            Price = 250000m,
            Currency = "EUR",
            Type = PropertyType.Villa,
            Transaction = TransactionType.Rent,
            City = "Pombal",
            District = "Leiria",
            Bedrooms = 3,
            AreaM2 = 200.0,
            Images = ["img1.jpg", "img2.jpg"],
            Status = PropertyStatus.Active,
        };

        Assert.Equal("test-123", listing.ExternalId);
        Assert.Equal("Beautiful Villa", listing.Title);
        Assert.Equal(250000m, listing.Price);
        Assert.Equal(PropertyType.Villa, listing.Type);
        Assert.Equal(TransactionType.Rent, listing.Transaction);
        Assert.Equal(3, listing.Bedrooms);
        Assert.Equal(200.0, listing.AreaM2);
        Assert.Equal(2, listing.Images.Count);
    }
}

public sealed class PropertyDefaultsTests
{
    [Fact]
    public void Default_Currency_IsEur()
    {
        var p = new Property();
        Assert.Equal("EUR", p.Currency);
    }

    [Fact]
    public void Default_City_IsPombal()
    {
        var p = new Property();
        Assert.Equal("Pombal", p.City);
    }

    [Fact]
    public void Default_District_IsLeiria()
    {
        var p = new Property();
        Assert.Equal("Leiria", p.District);
    }

    [Fact]
    public void Default_Status_IsActive()
    {
        var p = new Property();
        Assert.Equal(PropertyStatus.Active, p.Status);
    }

    [Fact]
    public void Default_Type_IsApartment()
    {
        var p = new Property();
        Assert.Equal(PropertyType.Apartment, p.Type);
    }
}

public sealed class PropertySearchCriteriaTests
{
    [Fact]
    public void Default_Page_IsOne()
    {
        var c = new PropertySearchCriteria();
        Assert.Equal(1, c.Page);
    }

    [Fact]
    public void Default_PageSize_IsTwenty()
    {
        var c = new PropertySearchCriteria();
        Assert.Equal(20, c.PageSize);
    }

    [Fact]
    public void Default_SortDescending_IsTrue()
    {
        var c = new PropertySearchCriteria();
        Assert.True(c.SortDescending);
    }

    [Fact]
    public void Default_SortBy_IsUpdatedAt()
    {
        var c = new PropertySearchCriteria();
        Assert.Equal("updatedAt", c.SortBy);
    }

    [Fact]
    public void Populated_Criteria_ReturnsValues()
    {
        var c = new PropertySearchCriteria
        {
            City = "Pombal",
            Type = PropertyType.House,
            Transaction = TransactionType.Sale,
            MinPrice = 100000m,
            MaxPrice = 500000m,
            MinBedrooms = 2,
            Page = 3,
            PageSize = 10,
            SortBy = "price",
            SortDescending = false,
        };

        Assert.Equal("Pombal", c.City);
        Assert.Equal(PropertyType.House, c.Type);
        Assert.Equal(TransactionType.Sale, c.Transaction);
        Assert.Equal(100000m, c.MinPrice);
        Assert.Equal(500000m, c.MaxPrice);
        Assert.Equal(2, c.MinBedrooms);
        Assert.Equal(3, c.Page);
        Assert.Equal(10, c.PageSize);
    }
}

public sealed class ScrapeErrorTests
{
    [Fact]
    public void Default_AgencyName_IsEmpty()
    {
        var err = new ScrapeError();
        Assert.Equal(string.Empty, err.AgencyName);
    }

    [Fact]
    public void Default_Message_IsEmpty()
    {
        var err = new ScrapeError();
        Assert.Equal(string.Empty, err.Message);
    }

    [Fact]
    public void Default_Severity_IsWarning()
    {
        var err = new ScrapeError();
        Assert.Equal(ScrapeErrorSeverity.Warning, err.Severity);
    }

    [Fact]
    public void Populated_Error_ReturnsValues()
    {
        var err = new ScrapeError
        {
            AgencyName = "Remax",
            ExternalId = "listing-001",
            Message = "HTTP 503 during detail fetch",
            ExceptionType = "HttpRequestException",
            Severity = ScrapeErrorSeverity.Error,
            Timestamp = new DateTime(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc),
        };

        Assert.Equal("Remax", err.AgencyName);
        Assert.Equal("listing-001", err.ExternalId);
        Assert.Equal("HTTP 503 during detail fetch", err.Message);
        Assert.Equal("HttpRequestException", err.ExceptionType);
        Assert.Equal(ScrapeErrorSeverity.Error, err.Severity);
    }
}

public sealed class ScraperSourceConfigTests
{
    [Fact]
    public void Default_Enabled_IsTrue()
    {
        var config = new ScraperSourceConfig();
        Assert.True(config.Enabled);
    }

    [Fact]
    public void Default_AgencyName_IsEmpty()
    {
        var config = new ScraperSourceConfig();
        Assert.Equal(string.Empty, config.AgencyName);
    }

    [Fact]
    public void Default_RequestDelay_Is1000Ms()
    {
        var config = new ScraperSourceConfig();
        Assert.Equal(1000, config.RequestDelayMs);
    }

    [Fact]
    public void Default_MaxRetries_Is3()
    {
        var config = new ScraperSourceConfig();
        Assert.Equal(3, config.MaxRetries);
    }

    [Fact]
    public void Default_PageSize_Is48()
    {
        var config = new ScraperSourceConfig();
        Assert.Equal(48, config.PageSize);
    }

    [Fact]
    public void Default_SearchParams_HasCity()
    {
        var config = new ScraperSourceConfig();
        Assert.Equal("Pombal", config.DefaultSearchParams["city"]);
    }
}
