# Manual de Observabilidade — CasaSim.pt

> **Versão:** 1.0  
> **Última atualização:** Junho 2026  
> **Idioma:** Português (pt-PT)

---

## 1. Visão Geral

A CasaSim.pt tem três camadas de observabilidade:

| Camada | Stack | O que cobre |
|---|---|---|
| **Logs estruturados** | Serilog + `ILogger` (JSON) | Pedidos HTTP, execução de scrapers, erros |
| **Tracing distribuído (OTEL)** | `OpenTelemetry` + `ActivitySource` | Pedidos à API, queries EF Core, ciclo de vida dos scrapers |
| **Métricas (OTEL)** | `Meter` + contadores/histogramas | Contagem de scrapers, listings descobertos/inseridos, durações |

Tudo é exportado via OTLP quando a variável `OTEL_EXPORTER_OTLP_ENDPOINT` está definida.  
Em produção (Coolify), por omissão não há backend OTEL — os logs do Docker são a fonte primária.

---

## 2. Logs

### 2.1. Onde ver os logs

Na dashboard do **Coolify** → aplicação `casasim-pt` → separador **Logs**.  
Também pode usar a CLI do Docker no servidor:

```bash
# Logs da API (últimas 100 linhas, follow)
docker logs casasim-pt-api-1 --tail 100 -f

# Logs do scraper
docker logs casasim-pt-scraper-1 --tail 100 -f

# Todos os serviços com timestamp
docker compose logs --tail=50 -f
```

### 2.2. Formato dos logs

Os logs são emitidos como JSON enriquecido. Exemplo real:

```json
{
  "@t": "2026-06-11T20:05:00Z",
  "@mt": "HTTP {Method} {Path} responded {StatusCode} in {Duration}ms",
  "Method": "GET",
  "Path": "/api/listings",
  "StatusCode": 200,
  "Duration": 45,
  "traceId": "abc123def456",
  "spanId": "789ghi",
  "SourceContext": "Serilog.AspNetCore.RequestLoggingMiddleware",
  "EnvironmentName": "Production"
}
```

Campos comuns em todos os serviços:

| Campo | Descrição |
|---|---|
| `@t` | Timestamp ISO 8601 |
| `@mt` | Template da mensagem |
| `SourceContext` | Classe/origem do log |
| `traceId` | ID de rastreio OTEL (quando ativo) |
| `EnvironmentName` | `Production` ou `Development` |

Campos específicos da **API**:

| Campo | Descrição |
|---|---|
| `Method` | Verbo HTTP (`GET`, `POST`, etc.) |
| `Path` | Rota pedida |
| `StatusCode` | Código de resposta |
| `Duration` | Tempo de resposta em ms |

Campos específicos do **scraper**:

| Campo | Descrição |
|---|---|
| `AgencySlug` | `remax`, `century21`, `era` |
| `ScraperPhase` | `Discover`, `Fetch`, `Parse`, `Upsert` |
| `ListingsFound` | Número de listings descobertos |
| `ListingsUpserted` | Número de listings inseridos/atualizados |

### 2.3. Níveis de log

| Nível | Uso |
|---|---|
| `Information` | Estado normal — pedidos, execução de scrapers |
| `Warning` | Comportamento inesperado não crítico (ex.: listing sem imagem) |
| `Error` | Falha recuperável (ex.: scraper não conseguiu aceder a um URL) |
| `Fatal` | Falha irrecuperável — aplicação vai terminar |

Os scrapers usam `LogLevel.Information` por omissão. Para mais detalhe durante debugging:

```bash
# Ativar debug no scraper (via variável de ambiente no Coolify)
DOTNET_LOG_LEVEL__DEFAULT=Debug
```

---

## 3. OpenTelemetry

### 3.1. Como ativar

A exportação OTEL está desligada por omissão. Para ativar, defina a variável de ambiente:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

No **Coolify**, adicione esta variável nas definições da aplicação (`casasim-pt`)  
com o endpoint do vosso collector (ex.: servidor Grafana Cloud OTLP ou self-hosted).

### 3.2. Traces (rastreios)

O que é tracedo automaticamente:

**API (`CasaSim.Api`):**
- Cada pedido HTTP → um trace com spans para:
  - Controller/Action
  - Queries EF Core / PostgreSQL
  - Chamadas HTTP externas (se houver)

**Scraper (`CasaSim.Scraper`):**
- Cada scraper run → span com fases:
  - `ScraperOrchestrator.RunScraperAsync` — span principal
  - `DiscoverListings` — span de descoberta de URLs
  - `ParseListing` — span de parsing de HTML
  - `UpsertListings` — span de inserção/atualização na BD

### 3.3. Métricas

Métricas emitidas pelo `ScraperDiagnostics` (`Meter = "CasaSim.Scraper"`):

| Métrica | Tipo | Tags |
|---|---|---|
| `scrape_runs_total` | Counter | `agency_slug`, `status` (success/failure) |
| `scrape_duration_seconds` | Histogram | `agency_slug` |
| `listings_discovered_total` | Counter | `agency_slug` |
| `listings_upserted_total` | Counter | `agency_slug`, `action` (created/updated) |
| `scrape_errors_total` | Counter | `agency_slug`, `phase`, `error_type` |

### 3.4. Local (desenvolvimento)

Para testar OTEL localmente sem um backend remoto:

```bash
# Sobe a stack completa + observabilidade
docker compose --profile observability up -d

# Jaeger UI: http://localhost:16686
# OTEL collector: localhost:4317 (gRPC) / localhost:4318 (HTTP)
```

A stack de observabilidade inclui:
- **otel-collector** — recebe OTLP, faz batch, reencaminha para Jaeger
- **Jaeger** — armazena e mostra traces numa interface web

Para testar:
1. Faça um pedido à API: `curl http://localhost:5000/api/listings`
2. Abra http://localhost:16686
3. Selecione o serviço `CasaSim.Api` e clique **Find Traces**

---

## 4. Scraper Diagnostics

### 4.1. Classe `ScraperDiagnostics`

Ficheiro: `src/backend/CasaSim.Scraper/Diagnostics/ScraperDiagnostics.cs`

```csharp
// ActivitySource para tracing
private static readonly ActivitySource ActivitySource = new("CasaSim.Scraper");

// Meter para métricas
private static readonly Meter Meter = new("CasaSim.Scraper", "1.0.0");
```

Métodos disponíveis:

| Método | O que faz | Cria |
|---|---|---|
| `StartScrapeSpan(agencySlug, phase)` | Inicia um span de tracing | Trace + span |
| `RecordScrapeRun(agencySlug, status)` | Incrementa contador de runs | Métrica |
| `RecordListingsDiscovered(agencySlug, count)` | Adiciona ao contador de descobertas | Métrica |
| `RecordListingsUpserted(agencySlug, action)` | Adiciona ao contador de upserts | Métrica |
| `RecordScrapeError(agencySlug, phase, errorType)` | Incrementa contador de erros | Métrica |

### 4.2. Como monitorizar scrapers

Para ver o estado dos scrapers em produção:

1. **Logs do Docker:**
   ```bash
   docker logs casasim-pt-scraper-1 --tail 50 -f
   ```

2. **BD (ScrapeLog):**
   ```sql
   SELECT * FROM "ScrapeLogs" ORDER BY "StartedAt" DESC LIMIT 10;
   ```

3. **Dashboard admin:** `https://casasim.pt/admin/scrapers`

4. **Com OTEL ativo:** consulte Jaeger ou Grafana com as métricas acima.

---

## 5. Checklist de Produção

Quando algo corre mal, siga esta ordem:

### 🔴 Site em baixo ou 500

1. Verificar health endpoint:
   ```bash
   curl https://casasim.pt/api/health
   ```
   Esperado: `{"status":"healthy","database":"connected"}`

2. Verificar logs da API:
   ```bash
   docker logs casasim-pt-api-1 --tail 100
   ```

3. Verificar se a BD responde:
   ```bash
   docker exec casasim-pt-db-1 pg_isready -U casasim
   ```

4. Se OTEL ativo, consultar traces em Jaeger/Grafana.

### 🟡 Scrapers não estão a correr

1. Verificar estado do scraper:
   ```bash
   docker logs casasim-pt-scraper-1 --tail 50
   ```

2. Verificar tabela `ScrapeLogs`:
   ```bash
   docker exec casasim-pt-db-1 psql -U casasim -d casasim -c "SELECT * FROM \"ScrapeLogs\" ORDER BY \"StartedAt\" DESC LIMIT 5;"
   ```

3. Verificar dashboard admin em `https://casasim.pt/admin`.

### 🟢 Listings desatualizados

1. Verificar hora do último scrape bem-sucedido.
2. Verificar se o scraper está agendado (cron job no Coolify / `IHostedService`).
3. Se necessário, forçar execução manual via admin dashboard.

---

## 6. Resolução de Problemas

### "Sem endpoint OTLP configurado" nos logs

Normal — a exportação OTEL é opcional. Para desativar o aviso:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT="" # vazio = desligado
```

### Traces não aparecem no Jaeger

1. Confirme que o perfil de observabilidade está ativo:
   ```bash
   docker compose ps
   ```
   Deve ver `otel-collector` e `jaeger`.

2. Confirme a variável `OTEL_EXPORTER_OTLP_ENDPOINT`:
   ```bash
   docker compose exec api env | grep OTEL
   ```

3. Verifique os logs do collector:
   ```bash
   docker compose logs otel-collector
   ```

### Scraper falha silenciosamente

1. Verifique os logs do scraper:
   ```bash
   docker logs casasim-pt-scraper-1 --tail 100
   ```

2. Verifique as métricas (com OTEL ativo):
   ```sql
   -- Últimos scrapes
   SELECT * FROM "ScrapeLogs" WHERE "Success" = false ORDER BY "StartedAt" DESC LIMIT 5;
   ```

3. Verifique se o site de origem está acessível do servidor:
   ```bash
   curl -I https://www.remax.pt
   ```

### Consumo de memória alto

O `otel-collector` tem `memory_limiter` configurado:
```yaml
memory_limiter:
  check_interval: 5s
  limit_mib: 512
  spike_limit_mib: 128
```

Se a memória do servidor for limitada, reduza estes valores no ficheiro `docker/otel-collector-config.yaml`.

---

## 7. Documentação Relacionada

- [Coolify — Logs & Debugging](https://coolify.io/docs/logs)
- [OpenTelemetry .NET SDK](https://opentelemetry.io/docs/languages/net/)
- [Jaeger Documentation](https://www.jaegertracing.io/docs/)
- [OpenTelemetry Collector](https://opentelemetry.io/docs/collector/)
- [Serilog Documentation](https://serilog.net/)
