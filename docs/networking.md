# Docker networking and API exposure

CasaSim.pt uses a private-by-default Docker Compose network model. The API is not meant to be exposed as an independent public service. Public traffic enters through the frontend/Nginx container, and Nginx proxies only `/api/...` requests to the API over Docker's private service network.

## Current Compose service exposure

`docker-compose.yml` defines these services:

| Service | Container port | Compose directive | Host-published by default? | Purpose |
| --- | ---: | --- | --- | --- |
| `frontend` | `80` | `expose: ["80"]` | No | Nginx static frontend and `/api` reverse proxy |
| `api` | `5000` | `expose: ["5000"]` | No | ASP.NET Core API |
| `db` | `5432` | `expose: ["5432"]` | No | PostgreSQL/PostGIS |
| `scraper` | none | none | No | Background scraper worker |

`expose` makes ports available to other containers on the Compose network. It does not publish the ports to the host. Host publication would require a `ports:` mapping, and the committed compose file intentionally does not define one.

## Public request flow

```text
https://casasim.pt/...
        |
        v
Coolify / reverse proxy route to frontend service
        |
        v
frontend container, Nginx port 80
        |\
        | \ static assets served from /usr/share/nginx/html
        |
        +-- /api/... -> proxy_pass http://api:5000
                              |
                              v
                       api container on private Docker network
                              |
                              v
                       db container on private Docker network
```

The frontend application uses relative API URLs (`baseURL: '/api'` in `src/frontend/src/lib/api.ts`). That keeps browser traffic same-origin and avoids requiring a separate public API hostname.

## Nginx API proxy

`nginx/default.conf` contains:

```nginx
location /api/ {
    proxy_pass http://api:5000;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header Connection "";
}
```

`api` is the Docker Compose service name. Docker DNS resolves it only inside the Compose network.

## Why the API is not publicly exposed directly

The design keeps a smaller public attack surface:

- Only one HTTP service needs to be routed publicly: `frontend`.
- The database has no host mapping and cannot be reached directly from the public internet through the Compose stack.
- The scraper has no public HTTP surface.
- The API is reachable by browsers only through frontend/Nginx `/api/...`, so public routing, TLS, headers, and same-origin behavior live in one place.
- Admin endpoints are additionally protected by `AdminSettings__ApiKey`, but network privacy is still required; API keys are not a substitute for avoiding unnecessary public exposure.

## Local development implications

Because the committed compose file has no `ports:` mappings, `docker compose up -d` alone does not make `http://localhost` or `http://localhost:5000` work from the host.

For local browser testing of the Docker stack, publish only the frontend via a temporary override:

```bash
cat > docker-compose.local.yml <<'YAML'
services:
  frontend:
    ports:
      - "8080:80"
YAML

docker compose -f docker-compose.yml -f docker-compose.local.yml up -d --build
```

Then use:

```text
http://localhost:8080
http://localhost:8080/api/health
```

Do not commit local overrides that publish `api:5000`, `db:5432`, or scraper ports.

## Operational checks

From a host with Docker installed:

```bash
# Render the fully-resolved compose config.
docker compose config

# Confirm only intentional host ports are published.
docker compose ps

# Check API health through private Docker DNS from another container.
docker compose exec frontend wget -qO- http://api:5000/api/health

# Check public path through the frontend when a frontend port/route exists.
curl -fsS http://localhost:8080/api/health
```

On Coolify, verify the same principle in the UI/API:

- The routed application should point at the `frontend` service/port.
- No public route should point directly at `api:5000`, `db:5432`, or the scraper.
- Environment variables such as `POSTGRES_PASSWORD` and `AdminSettings__ApiKey` should come from Coolify secrets/environment, not from committed files.

## Safe and unsafe changes

Safe:

- Publish `frontend:80` for local browser testing or production routing.
- Keep React API calls relative (`/api/...`).
- Add Nginx rules for public frontend behavior and selected API paths.

Unsafe unless explicitly needed for trusted debugging:

- Adding `ports: ["5000:5000"]` to `api`.
- Adding `ports: ["5432:5432"]` to `db` on a public host.
- Creating a public Coolify route to the API service.
- Hardcoding a public API origin in frontend code.
