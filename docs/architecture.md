# Architecture

CasaSim.pt is split into a React frontend, an ASP.NET Core API, a background scraper, and PostgreSQL/PostGIS storage. In production, the frontend container is the only public HTTP entrypoint; API traffic is reached through the frontend Nginx reverse proxy at `/api/...`.

## High-level runtime diagram

```text
Internet / Coolify route / browser
          |
          v
+-------------------------------+
| frontend container             |
| - Nginx on port 80             |
| - Serves Vite static assets    |
| - Proxies /api/ to api:5000    |
+-------------------------------+
          | private Docker DNS: http://api:5000
          v
+-------------------------------+          +-------------------------------+
| api container                  |          | scraper container              |
| - ASP.NET Core on port 5000    |          | - .NET worker process          |
| - Controllers under /api       |          | - Agency scrapers/parsers      |
| - EF Core migrations on start  |          | - Writes/upserts listings      |
+-------------------------------+          +-------------------------------+
          | private Docker DNS: db:5432                |
          +--------------------------+-----------------+
                                     v
                       +-------------------------------+
                       | db container                   |
                       | - postgis/postgis:16-3.4       |
                       | - PostgreSQL + PostGIS         |
                       | - postgres_data volume         |
                       +-------------------------------+
```

## Project layers

### Frontend: `src/frontend/`

React 19 + Vite + TypeScript single-page application.

Important paths:

- `src/frontend/src/main.tsx` - React entrypoint.
- `src/frontend/src/App.tsx` - application routes.
- `src/frontend/src/pages/` - route pages such as search, map, details, and admin.
- `src/frontend/src/components/` - shared UI and listing/property components.
- `src/frontend/src/hooks/` - TanStack Query data hooks.
- `src/frontend/src/lib/api.ts` - shared Axios client, configured with `baseURL: '/api'`.
- `src/frontend/vite.config.ts` - dev-server proxy from `/api` to `http://localhost:5000`.

Build/test commands:

```bash
cd src/frontend
npm ci
npm run lint
npm run build
npm run test
```

### API: `src/backend/CasaSim.Api/`

ASP.NET Core API with EF Core/Npgsql/PostGIS. The API listens on port `5000` in Docker (`docker/Dockerfile.api`) and maps controllers under `/api/...`.

Important paths:

- `Program.cs` - service registration, Swagger in development, CORS, EF migrations, controller mapping.
- `AppDbContext.cs` - EF Core database context.
- `Controllers/HealthController.cs` - `GET /api/health`.
- `Controllers/ListingsController.cs` - listing search and GeoJSON endpoints under `/api/listings`.
- `Controllers/PropertiesController.cs` - property search/detail endpoints under `/api/properties`.
- `Controllers/AdminController.cs` - admin endpoints under `/api/admin`, protected by `AdminAuthenticationFilter`.
- `Migrations/` - EF Core migrations.

Run locally from repo root:

```bash
export ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=casasim;Username=casasim;Password=changeme'
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://localhost:5000
export AdminSettings__ApiKey=dev-only-change-me

dotnet run --project src/backend/CasaSim.Api/CasaSim.Api.csproj
```

Swagger is registered only when `ASPNETCORE_ENVIRONMENT=Development`; it is not expected to be public in production.

For the full route/operator reference, including current query parameters, admin auth, and deployment checks, see `docs/api-and-deployment.md`.

### Core domain: `src/backend/CasaSim.Core/`

Shared domain models, entities, and interfaces used by both the API and scraper.

Important paths:

- `Data/Entities/` - persisted EF entities such as `Listing`, `Agency`, `Location`, and `ScrapeLog`.
- `Interfaces/` - scraper/repository/geocoding abstractions.
- `Models/` - parsed listing and scrape result models.

### Scraper: `src/backend/CasaSim.Scraper/`

.NET worker process that scrapes configured sources and writes normalized listings to PostgreSQL.

Important paths:

- `Program.cs` - worker bootstrap and hosted services.
- `Services/ScraperOrchestrator.cs` - coordinates scraper execution.
- `Services/*Scraper.cs` - agency-specific fetching.
- `Services/*ListingParser.cs` - agency-specific parsing.
- `Services/ListingUpsertService.cs` - database upsert path.
- `Services/ScrapeLoggingService.cs` - scrape run logging.
- `Configuration/ScraperSourceOptions.cs` - source configuration.
- `Fixtures/` - checked-in examples used for scraper development.

Run locally from repo root:

```bash
export ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=casasim;Username=casasim;Password=changeme'
dotnet run --project src/backend/CasaSim.Scraper/CasaSim.Scraper.csproj
```

### Database: PostgreSQL/PostGIS

The Compose stack uses `postgis/postgis:16-3.4` and stores data in the named volume `postgres_data`.

Important paths:

- `scripts/init-db.sql` - initialization script mounted into the database container.
- `src/backend/CasaSim.Api/Migrations/` - application schema migrations applied by the API on startup.

Docker connection string used by API and scraper containers:

```text
Host=db;Port=5432;Database=casasim;Username=${POSTGRES_USER:-casasim};Password=${POSTGRES_PASSWORD:-changeme}
```

## Request flow

1. Browser loads the React app from the `frontend` container.
2. React code calls relative URLs such as `/api/properties` or `/api/listings`.
3. In local Vite dev, `vite.config.ts` proxies `/api` to `http://localhost:5000`.
4. In Docker/production, `nginx/default.conf` proxies `/api/` to `http://api:5000` on the private Compose network.
5. The API queries PostgreSQL/PostGIS through EF Core and returns JSON/GeoJSON.
6. The scraper independently refreshes database content through the same database service name (`db`).

## Key production design decisions

- The frontend is the only intended public HTTP entrypoint.
- The API is private at the container/network level and reachable publicly only via selected `/api/...` paths proxied by Nginx.
- PostgreSQL is private to the Docker network.
- The scraper has no public HTTP interface.
- Admin endpoints require `AdminSettings__ApiKey`; production must set this to a strong secret through deployment environment variables.
