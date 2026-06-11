# CasaSim.pt

CasaSim.pt is a property aggregator for Pombal, Portugal. It collects listings from local real-estate agencies and exposes them through a searchable React application with an interactive map.

- Product domain: `casasim.pt`
- Repository: `github.com/Fjsmoreira/casasim-pt`
- Runtime model: Docker Compose stack with a public frontend/reverse proxy and private backend services

## Documentation

Start here:

- `docs/architecture.md` - application layers, project layout, data flow, and key routes.
- `docs/local-setup.md` - verified commands for local development, tests, and Docker smoke checks.
- `docs/networking.md` - Docker network model and why the API/database are not publicly exposed.
- `docs/api-and-deployment.md` - API route summary, admin auth, required deployment environment, and deploy checks.
- `docs/scrapers.md` - developer guide for adding agency scrapers, fixtures, tests, config, logging, and rate limits.
- `docs/scrapers/` - source-specific scraper notes.

## Repository layout

```text
casasim-pt/
├── CasaSim.sln                         # .NET solution, run from repo root
├── docker-compose.yml                  # Production/Coolify-oriented compose stack
├── docker/Dockerfile.api               # API image
├── Dockerfile.frontend                 # React build + Nginx image
├── Dockerfile.scraper                  # Background scraper image
├── nginx/default.conf                  # Frontend Nginx config and /api reverse proxy
├── scripts/init-db.sql                 # PostgreSQL/PostGIS initialization
├── docs/                               # Architecture, setup, API/deploy, networking, scraper docs
└── src/
    ├── backend/
    │   ├── CasaSim.Core/               # Domain entities, interfaces, parsed listing models
    │   ├── CasaSim.Api/                # ASP.NET Core API, EF Core, controllers
    │   ├── CasaSim.Api.Tests/          # API/core integration and service tests
    │   ├── CasaSim.Scraper/            # Background scraper/orchestrator
    │   └── CasaSim.Scraper.Tests/      # Scraper/parser tests
    └── frontend/                       # React 19 + Vite + TypeScript + Tailwind
```

## Quick start: Docker stack

The committed `docker-compose.yml` intentionally does not publish the API, PostgreSQL, or scraper to the host. It is designed for a reverse-proxy platform such as Coolify, where only the frontend/Nginx service is routed publicly.

Create an environment file:

```bash
cp .env.example .env
# Edit .env and set a strong AdminSettings__ApiKey before starting the API.
```

Start the stack:

```bash
docker compose up -d --build
```

Check container health from inside the Docker network:

```bash
docker compose ps
# API health from the frontend container through the private Docker service name:
docker compose exec frontend wget -qO- http://api:5000/api/health
# Frontend health inside its own container:
docker compose exec frontend wget -qO- http://127.0.0.1/
```

For a browser-accessible local Docker stack, add a temporary local override that publishes only the frontend:

```bash
cat > docker-compose.local.yml <<'YAML'
services:
  frontend:
    ports:
      - "8080:80"
YAML

docker compose -f docker-compose.yml -f docker-compose.local.yml up -d --build
# Open http://localhost:8080
# API calls still go through Nginx at http://localhost:8080/api/...
```

Do not publish the `api`, `db`, or `scraper` services unless you are deliberately debugging them on a trusted machine.

## Quick start: local development without Docker app containers

Use this when iterating on code locally. The examples assume PostgreSQL/PostGIS is available and that `ConnectionStrings__DefaultConnection` points at it.

Backend API:

```bash
# From repo root
export ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=casasim;Username=casasim;Password=changeme'
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://localhost:5000
export AdminSettings__ApiKey=dev-only-change-me

dotnet restore CasaSim.sln
dotnet run --project src/backend/CasaSim.Api/CasaSim.Api.csproj
```

Frontend dev server:

```bash
cd src/frontend
npm ci
npm run dev
# Open http://localhost:5173
# Vite proxies /api to http://localhost:5000.
```

Scraper:

```bash
# From repo root, with ConnectionStrings__DefaultConnection set
dotnet run --project src/backend/CasaSim.Scraper/CasaSim.Scraper.csproj
```

## Verification commands

Run these from the repository root unless noted otherwise:

```bash
# Backend restore/build/tests
dotnet restore CasaSim.sln
dotnet build CasaSim.sln --no-restore -c Release
dotnet test src/backend/CasaSim.Api.Tests/CasaSim.Api.Tests.csproj -c Release
dotnet test src/backend/CasaSim.Scraper.Tests/CasaSim.Scraper.Tests.csproj -c Release

# Frontend install/lint/build/tests
cd src/frontend
npm ci
npm run lint
npm run build
npm run test
```

Static Docker Compose validation:

```bash
docker compose config
```

## Network and public exposure summary

The public entrypoint is the frontend/Nginx service. Browser requests to `/api/...` are reverse-proxied by `nginx/default.conf` to `http://api:5000` over the private Compose network.

- `frontend` exposes container port `80`; publish or route this service only.
- `api` exposes container port `5000` to other containers but has no host `ports:` mapping.
- `db` exposes container port `5432` to other containers but has no host `ports:` mapping.
- `scraper` has no public HTTP surface.

See `docs/networking.md` for the full model and operational checks.


## Observability

### Structured Logging

API and scraper containers write structured JSON logs to stdout/stderr so Docker and Coolify can collect them without sidecar agents or local log files. In Coolify, open the CasaSim resource, choose the API or scraper service, and use the **Logs** tab for the live stream/history. Locally, use `docker compose logs -f api scraper`.

Common fields include `service.name`, `environment`, `requestId`, `traceId`, `route`, `statusCode`, and `durationMs` for API request completion logs. Scraper logs include `service.name`, `environment`, `scraperName`, `agencyName`, `agencySlug`, and, when a listing is being upserted, `sourceId` and `listingId`. Keep credentials in environment variables; do not log connection strings, API keys, cookies, or raw secrets.

Log levels can be tuned in `src/backend/CasaSim.Api/appsettings.json` and `src/backend/CasaSim.Scraper/appsettings.json` under `Serilog:MinimumLevel`, with environment overrides such as `Serilog__MinimumLevel__Default=Debug` in Docker/Coolify when temporary diagnostics are needed.

### Distributed Tracing & Metrics (otel-collector + Jaeger)

> ⚠️ This section covers the **local development** observability stack. In production, telemetry is shipped to Coolify's Otel endpoint; configure via `OTEL_EXPORTER_OTLP_ENDPOINT` in your Coolify service environment.

API and scraper are instrumented with OpenTelemetry — they export traces and metrics via OTLP whenever `OTEL_EXPORTER_OTLP_ENDPOINT` is configured. For local development, a compose profile adds an **OpenTelemetry Collector** and **Jaeger** backend:

```bash
# Start the full stack with local observability
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317 docker compose --profile observability up -d
```

#### What you get

| Service | Role | Ports |
|---|---|---|
| `otel-collector` | Receives OTLP from API/scraper, batches and forwards to Jaeger | `4317` (OTLP gRPC), `4318` (OTLP HTTP) |
| `jaeger` | Trace/metrics storage and Web UI | `16686` (Jaeger UI) |

#### URLs

| URL | What you see |
|---|---|
| `http://localhost:16686` | Jaeger UI — search traces, view service graph, inspect spans |
| `http://localhost:4318` | OTLP HTTP health check — returns `200` when collector is up |
| `http://localhost:80` | CasaSim frontend (as usual) |

#### Usage

1. **Start with observability:**
   ```bash
   OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317 docker compose --profile observability up -d
   ```

2. **Open Jaeger UI** at [http://localhost:16686](http://localhost:16686) — you'll see `CasaSim.Api` and `CasaSim.Scraper` in the service dropdown.

3. **Browse some listings** or **let the scraper run** to generate traces, then search in Jaeger.

4. **Without observability** (default `docker compose up -d`):
   The OTEL env var defaults to empty (`OTEL_EXPORTER_OTLP_ENDPOINT: ${OTEL_EXPORTER_OTLP_ENDPOINT:-}`), so the API and scraper skip OTLP export and run exactly as before — no overhead, no collector needed.

#### Traces emitted

**API (`CasaSim.Api`):**
- Incoming HTTP requests (automatic via `OpenTelemetry.Instrumentation.AspNetCore`)
- Outbound HTTP calls (automatic via `OpenTelemetry.Instrumentation.Http`)
- Database queries (automatic via `Npgsql.OpenTelemetry`)

**Scraper (`CasaSim.Scraper`):**
- `scraper.run` — full scrape cycle
- `scraper.upsert_batch` — database upsert batch

**Metrics emitted (Scraper):**
- `scrape_runs_total` — total scrape cycles (tags: scraper, agency_slug, status)
- `listings_discovered_total` — raw listings found (tags: scraper, agency_slug)
- `listings_upserted_total` — upserts by action (tags: scraper, agency_slug, action)
- `scrape_duration_seconds` — histogram of scrape cycle duration
- `scrape_errors_total` — scrape errors (tags: scraper, agency_slug)

#### Troubleshooting

| Symptom | Check |
|---|---|
| Jaeger shows "No services found" | Wait for a request to hit the API or the scraper to run its cycle |
| Collector won't start | Run `docker compose logs otel-collector` and verify the config at `docker/otel-collector-config.yaml` is valid YAML |
| "Connection refused" in API/scraper logs | The collector container isn't running — did you include `--profile observability`? |
| Port 4317/4318 already in use | Another process on your machine is using those ports. Either stop it or change the host port mapping in `docker-compose.yml` (e.g. `"14317:4317"`) and update your `.env` accordingly |

## Stack

- Backend: .NET 8, ASP.NET Core controllers, EF Core, Npgsql, Npgsql.NetTopologySuite, Serilog.
- Frontend: React 19, TypeScript, Vite, Tailwind CSS, TanStack Query, Zustand, Leaflet, React Router.
- Database: PostgreSQL 16 with PostGIS.
- Infrastructure: Docker Compose, Nginx reverse proxy, Coolify deployment.
