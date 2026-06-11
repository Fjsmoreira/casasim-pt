# ERA Portugal — Listing Source Discovery

> **Task:** t_11989134 — discover how to get ERA Pombal listings for Pombal  
> **Date:** 2026-06-11  
> **Status:** Research only — no parser implemented

---

## Overview

ERA Imobiliária is a Portuguese real estate franchise network. The website is an ASP.NET (DNN / DotNetNuke) SPA with a REST API at `https://www.era.pt/API/ServicesModule/`. The site renders search results entirely client-side via React; the API returns JSON data but requires a `__RequestVerificationToken` cookie and anti-CSRF header.

ERA Pombal (AMI 22767) is operated by "COISAS EXTRAORDINÁRIAS, LDA" and lists **112 properties** for sale (as of June 2026).

---

## Website URLs

### Search Pages

| Purpose | URL |
|---|---|
| Buy in Pombal | `https://www.era.pt/comprar?searchtext=Pombal&page=1` |
| Rent in Pombal | `https://www.era.pt/arrendar?searchtext=Pombal&page=1` |
| Agency page (ERA Pombal) | `https://www.era.pt/agencias/pombal` |
| Agency listings | `https://www.era.pt/imoveis/agencia/pombal?ord=3&ir=1&nr=0&page=1` |

### Listing Detail Pages

- **Pattern:** `https://www.era.pt/imovel/{slugified-title}-{property-id}`
- Property IDs are numeric (e.g., `404260053`, `1520789`, `1554198`).
- Slug is human-readable title with hyphens (e.g., `apartamento-t3-pombal-pombal`).

### Query Parameters for Agency / Search Pages

| Param | Description | Example |
|---|---|---|
| `searchtext` | Free-text search query | `Pombal` |
| `page` | Page number (1-based) | `1` |
| `ord` | Sort order | `3` (most common for agency listings) |
| `ir` | Include removed? | `1` |
| `nr` | New results? | `0` |

---

## API Endpoints

**Base URL:** `https://www.era.pt/API/ServicesModule/`

All API endpoints require:
- `__RequestVerificationToken` cookie (obtained from any initial GET to `era.pt`)
- `RequestVerificationToken` header (same value as cookie)
- `Content-Type: application/json`
- `X-Requested-With: XMLHttpRequest` header

### 1. Property Search — `POST /Property/Search`

The main endpoint for retrieving listings. Returns paginated JSON.

**Request body:**

```json
{
  "searchtext": "Pombal",
  "page": 1,
  "recordsPerPage": 15,
  "businessTypeIds": null,
  "propertyTypeIds": null,
  "propertySubTypeIds": null,
  "category": null,
  "agencyId": null,
  "order": null,
  "zoneIds": null,
  "vantagensERA": null,
  "projectIds": null,
  "minPrice": null,
  "maxPrice": null,
  "minArea": null,
  "maxArea": null
}
```

The exact request payload format may need further reverse-engineering — the API returns 401 without proper anti-CSRF headers (standard ASP.NET MVC anti-forgery).

### 2. Count Properties — `POST /Property/CountProperties`

Returns total count of matching properties (filters same as Search).

### 3. Reference Data — `GET /ReferenceData/SearchPageReferenceData`

Returns reference/lookup data for search filters (zone lists, property types, etc.).

### 4. Analytics — `POST /analytics/list`

Tracks listing impressions/views.

---

## ERA Pombal Agency Details

| Field | Value |
|---|---|
| Name | ERA Pombal |
| Company | COISAS EXTRAORDINÁRIAS, LDA |
| AMI | 22767 |
| Address | Largo 25 de Abril 14, 3100-468 Pombal |
| Phone | +351 236 096 000 |
| Email | pombal@era.pt |
| Manager | Luciano Oliveira |
| Listings count | 112 properties |

---

## Example Property URLs

1. **T3 Apartment in Pombal (€258,000):**
   `https://www.era.pt/imovel/apartamento-t3-pombal-pombal-404260053`

2. **T3 House in Abiul, Pombal:**
   `https://www.era.pt/imovel/moradia-t3-pombal-abiul-404260052`

3. **T2 House in Pombal:**
   `https://www.era.pt/imovel/moradia-t2-pombal-404260051`

4. **T3 House in Pelariga, Pombal:**
   `https://www.era.pt/imovel/moradia-t4-pombal-pelariga-404260050`

5. **T2 House in Redinha, Pombal:**
   `https://www.era.pt/imovel/moradia-t2-pombal-redinha-404260049`

---

## Property Detail Page Fields

From the rendered detail page, each listing has:

| Field | Example |
|---|---|
| Reference | `404260053` |
| Transaction type | Comprar (Buy) / Arrendar (Rent) |
| Price | `258.000 €` |
| Property type | Apartamento / Moradia / Terreno / etc. |
| Bedrooms | `3` |
| Bathrooms | `2` |
| Usable area (m²) | `101` |
| Gross private area (m²) | `149` |
| Parking spaces | `1` |
| Floor | `2` |
| Energy certificate | `C` |
| Price per m² | `1.732 €` |
| Location | `POMBAL, Pombal` |
| Agency | ERA Pombal (pombal@era.pt, 236 096 000) |

---

## Implementation Notes (for future parser)

- **Authentication:** The API uses ASP.NET anti-forgery tokens. A two-step approach is needed:
  1. GET the search page or homepage → extract `__RequestVerificationToken` cookie
  2. POST to the API with the cookie + matching `RequestVerificationToken` header
- **Rate limiting:** Not observed during testing — behind Cloudflare CDN.
- **Client-side rendering:** Search pages load via React JS after page load — no SSR listing data in HTML.
- **Agency-scoped listing page:** `https://www.era.pt/imoveis/agencia/pombal` is the most targeted URL for ERA Pombal listings.
- **Pagination:** Query parameter `&page=N` on the search/agency URL. Default is 15 records per page.
- **Alternative approach:** Puppeteer/Playwright can render the SPA and scrape listing cards from the DOM — avoids the anti-forgery token issue entirely.
- **ERA data model differences:** Unlike Century21's clean REST API, ERA's API is less documented and may need payload reverse-engineering. A headless-browser approach may be more reliable than direct API calls.
