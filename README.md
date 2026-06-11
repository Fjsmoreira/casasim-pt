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

## Stack

- **Backend**: .NET 8, EF Core + Npgsql, Npgsql.NetTopologySuite, Serilog
- **Frontend**: React 19, TypeScript, Vite, Tailwind CSS, Leaflet, React Router
- **Database**: PostgreSQL 16 + PostGIS
- **Infra**: Docker Compose, Nginx reverse proxy (API not publicly exposed)
