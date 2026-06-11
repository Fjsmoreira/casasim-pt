# CasaSim.pt

**Property aggregator for Pombal, Portugal.** Inspired by Funda.nl — aggregates buying and renting listings from local real estate agencies into a single searchable platform with an interactive map.

> **Product:** casasim.pt  
> **Repo:** github.com/Fjsmoreira/casasim-pt

## Architecture

```
casasim-pt/
├── src/
│   ├── backend/              # .NET 8 solution
│   │   ├── CasaSim.Core/     # Domain models, interfaces, services
│   │   ├── CasaSim.Api/      # Web API (REST + Swagger)
│   │   └── CasaSim.Scraper/  # Background scraper (Hosted Service)
│   └── frontend/             # React + Vite + TypeScript + Tailwind
│       ├── src/
│       │   ├── components/   # Shared UI components
│       │   ├── pages/        # Route pages
│       │   ├── lib/          # Utilities
│       │   └── hooks/        # React hooks
│       └── package.json
├── nginx/                    # Reverse proxy config
├── scripts/                  # DB init scripts
├── docker-compose.yml        # Full stack (db + api + scraper + frontend)
├── Dockerfile.api
├── Dockerfile.scraper
├── Dockerfile.frontend
└── .github/workflows/ci.yml
```

## Quick Start

```bash
# Copy env template (edit passwords as needed)
cp .env.example .env

# Start everything
docker compose up -d

# Frontend:   http://localhost
# API:        http://localhost/api (proxied via frontend/Nginx)
# Swagger:    http://localhost/swagger
```

The stack uses an internal networking model — only the **frontend** (port 80) is published to the host. The **API** (port 5000), **PostgreSQL** (port 5432), and **scraper** are reachable from other containers by their Docker service name (`api`, `db`, `scraper`) but are not directly accessible from the host.

## Development

### Backend

```bash
# Requires .NET 8 SDK
cd src/backend
dotnet restore
dotnet run --project CasaSim.Api
```

### Frontend

```bash
cd src/frontend
npm install
npm run dev
```

The frontend dev server proxies `/api` to `http://localhost:5000` (configured in `vite.config.ts`).


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

- **Backend**: .NET 8, EF Core + Npgsql, Npgsql.NetTopologySuite, Serilog
- **Frontend**: React 19, TypeScript, Vite, Tailwind CSS, Leaflet, React Router
- **Database**: PostgreSQL 16 + PostGIS
- **Infra**: Docker Compose, Nginx reverse proxy (API not publicly exposed)
