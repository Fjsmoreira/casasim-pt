# Local setup

This page documents the commands that match the current repository layout. Run commands from the repository root unless a `cd` is shown.

## Prerequisites

- Docker with the Compose plugin (`docker compose ...`) for the container stack.
- .NET 8 SDK for backend/API/scraper development.
- Node.js 20+ and npm for the frontend.
- PostgreSQL/PostGIS if running the API or scraper directly on the host instead of fully inside Docker.

## Environment file

Create a local environment file from the template:

```bash
cp .env.example .env
```

For Docker, edit `.env` and set at least:

```text
POSTGRES_USER=casasim
POSTGRES_PASSWORD=<local password>
POSTGRES_DB=casasim
AdminSettings__ApiKey=<strong local admin API key>
```

`docker-compose.yml` fails fast if `AdminSettings__ApiKey` is unset because the API service uses:

```yaml
AdminSettings__ApiKey: ${AdminSettings__ApiKey:?AdminSettings__ApiKey is required}
```

## Option A: full Docker stack

Start the stack:

```bash
docker compose up -d --build
```

Check status:

```bash
docker compose ps
```

Check the private services from inside the Docker network:

```bash
# API health endpoint from the frontend container via Docker service DNS
docker compose exec frontend wget -qO- http://api:5000/api/health

# Frontend/Nginx inside the frontend container
docker compose exec frontend wget -qO- http://127.0.0.1/
```

Stop the stack without deleting the database volume:

```bash
docker compose down
```

Stop the stack and delete the local database volume:

```bash
docker compose down -v
```

### Open the Docker stack in a local browser

The committed compose file is production/Coolify-oriented and does not include host `ports:` mappings. To open it from a local browser, create a temporary override that publishes only the frontend:

```bash
cat > docker-compose.local.yml <<'YAML'
services:
  frontend:
    ports:
      - "8080:80"
YAML

docker compose -f docker-compose.yml -f docker-compose.local.yml up -d --build
```

Then open:

```text
http://localhost:8080
http://localhost:8080/api/health
```

Keep API, PostgreSQL, and scraper private. If you temporarily publish them for debugging, do not commit that override and do not use it on a public host.

## Option B: local API and frontend dev servers

Use this when actively developing code. You need a PostgreSQL/PostGIS database reachable from your host, then run API and frontend as host processes.

### API

```bash
export ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=casasim;Username=casasim;Password=changeme'
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://localhost:5000
export AdminSettings__ApiKey=dev-only-change-me

dotnet restore CasaSim.sln
dotnet run --project src/backend/CasaSim.Api/CasaSim.Api.csproj
```

Development URLs:

```text
http://localhost:5000/api/health
http://localhost:5000/swagger
```

Swagger is enabled only when `ASPNETCORE_ENVIRONMENT=Development`.

### Frontend

In a second terminal:

```bash
cd src/frontend
npm ci
npm run dev
```

Open:

```text
http://localhost:5173
```

The Vite dev server proxies `/api` to `http://localhost:5000` through `src/frontend/vite.config.ts`.

### Scraper

Run the scraper with the same database connection string used by the API:

```bash
export ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=casasim;Username=casasim;Password=changeme'
dotnet run --project src/backend/CasaSim.Scraper/CasaSim.Scraper.csproj
```

## Tests and checks

For route details, admin API-key usage, and deployment smoke checks, see `docs/api-and-deployment.md`.

Backend:

```bash
dotnet restore CasaSim.sln
dotnet build CasaSim.sln --no-restore -c Release
dotnet test src/backend/CasaSim.Api.Tests/CasaSim.Api.Tests.csproj -c Release
dotnet test src/backend/CasaSim.Scraper.Tests/CasaSim.Scraper.Tests.csproj -c Release
```

Frontend:

```bash
cd src/frontend
npm ci
npm run lint
npm run build
npm run test
```

Docker Compose static validation:

```bash
docker compose config
```

## Common pitfalls

- Run `dotnet restore CasaSim.sln` and `dotnet build CasaSim.sln` from the repository root. The solution file is not under `src/backend/`.
- The Docker Compose stack does not publish ports by default. Use the local override above if you need browser access on `localhost`.
- The API Docker container listens on port `5000`, but that port is only exposed to other containers unless you add a host `ports:` mapping.
- The frontend Docker image uses Nginx and proxies `/api/` to `http://api:5000`; React should call relative `/api/...` URLs, not a hardcoded public API host.
- `AdminSettings__ApiKey` should be non-empty for any admin endpoint testing and strong in production.
