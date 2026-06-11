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
├── docs/                               # Architecture, setup, networking, scraper docs
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

## Stack

- Backend: .NET 8, ASP.NET Core controllers, EF Core, Npgsql, Npgsql.NetTopologySuite, Serilog.
- Frontend: React 19, TypeScript, Vite, Tailwind CSS, TanStack Query, Zustand, Leaflet, React Router.
- Database: PostgreSQL 16 with PostGIS.
- Infrastructure: Docker Compose, Nginx reverse proxy, Coolify deployment.
