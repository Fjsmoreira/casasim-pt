# Scraper developer guide

This guide explains how to add or maintain an agency scraper in CasaSim.pt. It is written against the current implementation in `src/backend/CasaSim.Scraper`, `src/backend/CasaSim.Scraper.Tests`, and shared scraper models in `src/backend/CasaSim.Core`.

## Current scraper architecture

The scraper app is a .NET 10 background worker:

- Entry point: `src/backend/CasaSim.Scraper/Program.cs`
- Runtime scheduler: `Services/ScraperOrchestrator.cs`
- Agency scrapers: `Services/*Scraper.cs`
- Parsers: `Services/*ListingParser.cs`
- Per-source config: `Configuration/ScraperSourceOptions.cs`
- App config: `src/backend/CasaSim.Scraper/appsettings.json`
- Parser/scraper tests: `src/backend/CasaSim.Scraper.Tests/`
- Test fixtures: `src/backend/CasaSim.Scraper.Tests/Fixtures/`
- Legacy/development Remax examples: `src/backend/CasaSim.Scraper/Fixtures/`

`Program.cs` registers all scraper services in DI, then starts `ScraperOrchestrator` as a hosted service. The orchestrator reads `ScraperSources` configuration, finds enabled source names, resolves registered `IPropertyScraper` implementations by `AgencyName`, and runs due sources sequentially on a `PeriodicTimer`.

The production path currently uses `IPropertyScraper`:

```csharp
public interface IPropertyScraper
{
    string AgencyName { get; }
    Task<IReadOnlyList<Property>> ScrapeAsync(CancellationToken ct = default);
}
```

Some scrapers also implement the richer `IAgencyScraper` interface for search/detail-oriented workflows and tests, but `ScraperOrchestrator` currently calls `IPropertyScraper.ScrapeAsync` and then upserts returned `Property` objects through `ListingUpsertService`.

## Existing source patterns

Use the current scrapers as templates:

- `Century21Scraper` calls the Century21 public JSON API twice per cycle (`sell` and `rent`). The list endpoint already includes full listing data, so there is no separate detail fetch. `Century21ListingParser` turns API JSON into `ParsedListing`, and the scraper maps it to `Property`.
- `EraScraper` acquires an anti-forgery token, posts to the ERA search API, then fetches detail pages and parses HTML with `EraListingParser`.
- `RemaxScraper` is still a minimal scraper shell. Its parser (`RemaxListingParser`) and fixtures are more complete than the live scraper.

Before adding a new source, decide which of these patterns it matches:

1. API-only source: list/search API returns enough fields to build a `Property`.
2. Search + detail source: search returns IDs/URLs, details require one extra request per listing.
3. HTML-only source: parse listing cards and detail pages with `HtmlAgilityPack` or another parser already referenced by the project.

## Step 1: create a parser first

Create a parser class under `src/backend/CasaSim.Scraper/Services`, for example:

```text
src/backend/CasaSim.Scraper/Services/AcmeListingParser.cs
```

Keep parser code deterministic and side-effect free:

- Input should be raw JSON or HTML plus an optional source URL.
- Output should be `ParsedListing?`, `IReadOnlyList<ParsedListing>`, or `Property?`, depending on the source pattern.
- Do not perform HTTP calls in the parser.
- Normalize fields in one place: price, transaction, property type, area, land area, bedrooms, bathrooms, coordinates, images, external ID, listing URL, and text fields.
- Return `null` or skip a listing when required identity fields are missing. At minimum a listing needs a stable external ID and enough data for a useful listing.

Prefer parser tests that cover real-world variants over a parser that only handles one perfect fixture.

## Step 2: add fixtures

Put test fixtures in:

```text
src/backend/CasaSim.Scraper.Tests/Fixtures/
```

The test project already copies that directory to the test output with:

```xml
<Content Include="Fixtures/**/*" CopyToOutputDirectory="PreserveNewest" />
```

Fixture naming convention used today:

```text
<agency>-<kind>-<external-id>.json
<agency>-<kind>-<external-id>.html
<agency>-search-response.json
<agency>-sell-list.json
<agency>-rent-list.json
```

Examples:

- `century21-house-0563-01902.json`
- `century21-sell-list.json`
- `era-apartment-404260053.html`
- `era-search-response.json`
- `remax-listing-122591135-5.json`
- `remax-detail-122591135-5.html`

Fixture guidelines:

- Use publicly accessible pages or API responses only.
- Capture the smallest fixture that still represents the real response shape.
- Do not include secrets, cookies, auth headers, personal data, or private account output.
- Keep both successful and edge-case fixtures when the source has multiple property types or missing fields.
- If you fetch live data manually, use a normal User-Agent, make only the requests needed for the fixture, and respect the source site's robots.txt and acceptable-use constraints.

The older `src/backend/CasaSim.Scraper/Fixtures/` directory contains Remax development examples and its own README. For new automated tests, prefer the test fixture directory above.

## Step 3: write parser tests

Add xUnit tests under:

```text
src/backend/CasaSim.Scraper.Tests/
```

Follow the current test style:

```csharp
public sealed class AcmeListingParserTests
{
    private const string FixturesDir = "Fixtures";

    [Fact]
    public void ParseFromJson_RealListing_MapsAllFields()
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir, "acme-house-123.json"));
        var parser = new AcmeListingParser();

        var listing = parser.ParseFromJson(json, "https://example.test/listing/123");

        Assert.NotNull(listing);
        Assert.Equal("123", listing!.ExternalId);
        Assert.Equal("Acme", listing.SourceAgency);
        Assert.Equal("EUR", listing.Currency);
        Assert.True(listing.Price >= 0);
        Assert.False(string.IsNullOrWhiteSpace(listing.Title));
    }
}
```

Cover at least:

- External ID extraction.
- Source agency name.
- Listing URL.
- Price and currency.
- Property type and transaction type.
- City, district, postal code, and address behavior.
- Bedrooms, bathrooms, floor area, and land area when present.
- Coordinates and SRID 4326 when present.
- Images, including de-duplication and absolute URL conversion.
- Missing optional fields.
- Invalid/empty input behavior.

If the scraper uses a search API, add tests like `EraScraperTests.ParseSearchResponse_*` for search response parsing.

## Step 4: create the agency scraper

Add a scraper class under `src/backend/CasaSim.Scraper/Services`, for example:

```text
src/backend/CasaSim.Scraper/Services/AcmeScraper.cs
```

Implement `IPropertyScraper` because that is what the orchestrator runs today:

```csharp
internal sealed class AcmeScraper : IPropertyScraper
{
    public string AgencyName => "Acme";

    private readonly HttpClient _http;
    private readonly AcmeListingParser _parser;
    private readonly ILogger<AcmeScraper> _logger;

    public AcmeScraper(HttpClient http, AcmeListingParser parser, ILogger<AcmeScraper> logger)
    {
        _http = http;
        _parser = parser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Property>> ScrapeAsync(CancellationToken ct = default)
    {
        var properties = new List<Property>();

        // Fetch search/list pages, parse results, fetch details if needed,
        // log per-listing failures, and keep processing the rest.

        _logger.LogInformation("Acme total: {Count} properties", properties.Count);
        return properties;
    }
}
```

Important implementation rules:

- `AgencyName` must match the key you add under `ScraperSources` in `appsettings.json`.
- Pass `CancellationToken` to every HTTP and async call.
- Call `response.EnsureSuccessStatusCode()` when a failed response should fail that request.
- Catch per-listing detail failures inside the loop so one broken listing does not abort the whole source.
- Log useful source-level and listing-level context with structured logging placeholders, not string concatenation.
- Return an empty list when the source is reachable but has no parseable listings.
- Do not write directly to the database in the scraper; the orchestrator handles `ListingUpsertService.UpsertBatchAsync`.

If the source benefits from search/detail methods, also implement `IAgencyScraper`. Keep in mind that this is not the orchestrator path yet; it is useful for tests and future workflows.

## Step 5: map agency slug for upserts

`ScraperOrchestrator` calls:

```csharp
var agencySlug = ListingUpsertService.ResolveAgencySlug(agencyName);
```

If the new agency name is not mapped there, the orchestrator logs a warning and skips the source before scraping. Update `ListingUpsertService.ResolveAgencySlug` with the new `AgencyName` to database slug mapping.

Verify that the target agency row exists in seed data or the production database with the same slug, otherwise scrape logs and listing upserts cannot attach to the correct agency.

## Step 6: register DI services

Update `src/backend/CasaSim.Scraper/Program.cs`:

```csharp
services.AddScoped<IPropertyScraper, AcmeScraper>();
services.AddScoped<AcmeListingParser>();
```

Use `AddScoped` to match the existing scrapers. The orchestrator creates a fresh scope per tick and resolves the registered `IPropertyScraper` implementations from that scope.

## Step 7: add source configuration

Update `src/backend/CasaSim.Scraper/appsettings.json`:

```json
"ScraperSources": {
  "Acme": {
    "Interval": "06:00:00",
    "Enabled": true
  }
}
```

Configuration is bound to `Dictionary<string, ScraperSourceOptions>`. The supported fields are:

- `Interval`: .NET `TimeSpan` format. Existing sources use `"06:00:00"` for six hours.
- `Enabled`: whether the orchestrator should consider the source.

The scheduler uses the shortest enabled interval as the timer tick and then checks each source's own interval. It re-reads config while running, so disabling a source or changing its interval can take effect without rebuilding the app. New source discovery itself happens at startup, so adding a completely new source still requires a restart.

For local or production overrides, use normal .NET configuration keys such as:

```bash
ScraperSources__Acme__Enabled=false
ScraperSources__Acme__Interval=12:00:00
```

## Step 8: logging and scrape logs

There are two logging layers:

1. Console/application logs through `ILogger<T>`.
2. Database scrape logs through `ScrapeLoggingService`, called by `ScraperOrchestrator`.

Scraper classes should use `ILogger<T>` for operational detail:

```csharp
_logger.LogInformation("Acme search found {Count} listing(s)", results.Count);
_logger.LogError(ex, "Failed to fetch/parse Acme listing {Id} at {Url}", id, url);
```

Do not call `ScrapeLoggingService` from the agency scraper. The orchestrator already creates one scrape log per source run, marks it succeeded with found/created/updated/skipped counts after upsert, or marks it failed with exception details if the full scraper/upsert path throws.

Use `LogDebug` for full URLs or verbose diagnostics, `LogInformation` for source-level counts, `LogWarning` for recoverable missing configuration/mapping, and `LogError` for request or parser failures that prevent one listing or one source phase from succeeding.

## Step 9: rate limits and polite crawling

There is no central rate-limit helper in the current implementation. Each scraper is responsible for keeping request volume low and source-appropriate.

Current behavior:

- `Century21Scraper` makes two API calls per cycle: sell and rent.
- `EraScraper` fetches a token, performs one search request, then fetches detail pages for returned listings.
- `ScraperOrchestrator` runs sources sequentially and only at configured intervals.

When adding a source:

- Prefer official/public JSON endpoints over scraping heavy HTML pages when available.
- Keep the default interval conservative, usually six hours or more unless there is a product reason to run faster.
- Avoid unbounded pagination. Set explicit page sizes and maximum pages when a site can return many pages.
- Add per-detail delays with `Task.Delay(..., ct)` if a source requires many detail requests.
- Use conditional requests or change detection if the source exposes ETags, timestamps, or listing update metadata.
- Do not parallelize detail requests unless the source explicitly allows it and tests prove the implementation handles failures cleanly.
- Respect robots.txt and website terms.
- Never commit cookies, session tokens, API keys, or authenticated responses as fixtures.

If you introduce a reusable rate-limit or retry helper later, document it here and use it from every scraper instead of creating one-off throttling logic.

## Step 10: run verification

Before committing a scraper change, run at least:

```bash
dotnet test src/backend/CasaSim.Scraper.Tests/CasaSim.Scraper.Tests.csproj -c Release
```

When shared models, upsert behavior, or API-visible listing fields change, also run broader backend tests:

```bash
dotnet test CasaSim.sln -c Release
```

For docs-only edits to this guide, verify that referenced files still exist and that the commands match the project structure.

## Checklist for a new agency scraper

- [ ] Parser class added under `CasaSim.Scraper/Services`.
- [ ] Scraper class added under `CasaSim.Scraper/Services` and implements `IPropertyScraper`.
- [ ] Parser and scraper registered in `Program.cs`.
- [ ] `ScraperSources:<AgencyName>` config added to `appsettings.json`.
- [ ] `ListingUpsertService.ResolveAgencySlug` maps `AgencyName` to the correct agency slug.
- [ ] Test fixtures added under `CasaSim.Scraper.Tests/Fixtures`.
- [ ] Parser tests cover all important fields and edge cases.
- [ ] Scraper tests use fake `HttpMessageHandler` responses instead of live network calls.
- [ ] Logs include source-level counts and per-listing failure context.
- [ ] Request volume is bounded by config interval, pagination limits, and per-detail delays where needed.
- [ ] `dotnet test src/backend/CasaSim.Scraper.Tests/CasaSim.Scraper.Tests.csproj -c Release` passes.
