# API and deployment notes

This page is the operator/developer reference for CasaSim.pt API routes, public exposure, required environment, and deployment checks. It complements `docs/architecture.md`, `docs/local-setup.md`, and `docs/networking.md`.

## Public entrypoint

The intended public entrypoint is the frontend/Nginx service. Browsers call same-origin paths such as `/api/listings`, and `nginx/default.conf` proxies `/api/` to the private Docker service `http://api:5000`.

Do not create a public route or host `ports:` mapping directly to the API, PostgreSQL, or scraper services unless you are doing temporary trusted debugging.

## API route summary

All routes are mounted under `/api`.

| Area | Method/path | Notes |
| --- | --- | --- |
| Health | `GET /api/health` | Database-backed health endpoint used by container and deployment checks. |
| Listings | `GET /api/listings` | Preferred public listing search endpoint. Returns a paged result of listing summaries. |
| Properties | `GET /api/properties` | Legacy/public property search endpoint returning `{ items, total, page, pageSize }`. |
| Properties | `GET /api/properties/{id}` | Listing detail by GUID. |
| Admin | `GET /api/admin/ping` | Admin connectivity check; requires API key. |
| Admin | `GET /api/admin/dashboard` | Summary counts for the admin dashboard; requires API key. |
| Admin | `GET /api/admin/listings` | Admin listing table with pagination and filters; requires API key. |
| Admin | `GET /api/admin/agencies` | Agency list for admin filters; requires API key. |
| Admin | `GET /api/admin/scraper-status` | Latest scrape run/status summary; requires API key. |
| Admin | `GET /api/admin/scrape-runs/active` | Currently running scraper runs; requires API key. |
| Admin | `GET /api/admin/scrape-runs/{id}/activity` | Operator-facing run milestones, optionally after a timestamp; requires API key. |

Swagger is registered only in development (`ASPNETCORE_ENVIRONMENT=Development`) and should not be expected on production routes.

## Public listing search parameters

`GET /api/listings` accepts the current `ListingSearchRequest` query parameters:

- Filters: `city`, `propertyType`, `priceType`, `status`, `minPrice`, `maxPrice`, `minBedrooms`, `minAreaM2`, `agencySlug`, `dealLabel`.
- Sorting: `sortBy`, `sortDirection`.
- Pagination: `page`, `pageSize`.

Listing summary/detail responses include an optional `ai` object with generated summary, deal score/label, reasons, warnings, and corrected facts.

## Admin authentication

Admin routes are protected by `AdminAuthenticationFilter` and require `AdminSettings__ApiKey` to be configured. Set it through environment/secrets in local `.env`, Docker Compose, or Coolify. Do not commit real API keys.

For local testing, send the configured key as an `X-Api-Key` request header. If this header contract changes, update this page and `src/backend/CasaSim.Api/Auth/AdminAuthenticationFilter.cs` together.

## Required deployment environment

The Compose stack expects these values at minimum:

```text
POSTGRES_USER=casasim
POSTGRES_PASSWORD=strong-random-password
POSTGRES_DB=casasim
AdminSettings__ApiKey=strong-random-admin-api-key
```

`docker-compose.yml` currently defaults the database name to `casasim` for the API and scraper connection strings. Keep the database service, API service, and scraper service aligned if you change the database name.

## Docker/Coolify service model

The Compose stack includes a one-shot `migrate` service. It runs the EF Core migration bundle after PostgreSQL is healthy; the API and scraper wait for it to finish successfully. It is run on every deploy and exits without changes when no migrations are pending. Do not move migrations into normal API startup.

For a deliberate clean data rebuild, run the scraper with the guarded operator command:

```bash
dotnet run --project src/backend/CasaSim.Scraper/CasaSim.Scraper.csproj -- --rebuild-data --confirm-drop
```

This drops and recreates the configured database through migrations, seeds agencies and scraper sources from migrations, runs every registered scraper once, runs AI enrichment for active listings when AI is enabled and configured, and prints counts by agency plus failed scraper/AI counts. It is never run automatically at startup.

`docker-compose.yml` defines four services:

- `frontend`: Vite build served by Nginx on container port `80`; this is the only service that should be routed publicly.
- `api`: ASP.NET Core API on container port `5000`; private to the Compose network.
- `db`: PostgreSQL/PostGIS on container port `5432`; private to the Compose network with data in the `postgres_data` volume.
- `scraper`: .NET background worker; no public HTTP surface.

For Coolify, route the application/domain to the `frontend` service and port `80`. Environment variables and secrets should be configured in Coolify, not committed into the repository.

## Deployment checks

Before or after deploying, use these checks from a machine with Docker access:

```bash
# Render and validate the resolved Compose file.
docker compose config

# Confirm only the intended service is routed/published.
docker compose ps

# Check the private API health path from inside the frontend container.
docker compose exec frontend wget -qO- http://api:5000/api/health

# If the frontend is published locally or routed by Coolify, verify the public proxy path.
curl -fsS https://casasim.pt/api/health
```

For a local browser smoke test with the committed production-oriented Compose file, use the temporary frontend-only override described in `docs/local-setup.md` and `docs/networking.md`, then check:

```text
http://localhost:8080
http://localhost:8080/api/health
```

## Common deployment pitfalls

- Publishing `api:5000` or `db:5432` directly widens the attack surface and bypasses the intended Nginx entrypoint.
- A missing `AdminSettings__ApiKey` prevents the API service from starting because Compose uses a required environment expansion.
- Frontend code should use relative `/api/...` URLs. Hardcoded public API origins break the same-origin deployment model.
- A successful frontend page load does not prove API/database health; always verify `/api/health` through the public proxy and from inside the Docker network.
- Scraper health only proves that the worker process is running. Use `/api/admin/scraper-status` with the admin key to inspect recent scraper outcomes.
