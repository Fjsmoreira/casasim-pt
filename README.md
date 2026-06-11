# CasaSim.pt

Property aggregator for Pombal, Leiria — Portugal. Aggregates real estate listings from multiple agencies (Remax, etc.) into a single searchable platform with an interactive map.

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
# Copy env template
cp .env.example .env

# Start everything
docker compose up -d

# API: http://localhost/api
# Swagger: http://localhost/swagger
# Frontend: http://localhost
```

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
