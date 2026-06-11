# Century21 Portugal — Listing Source Discovery

> **Task:** t_c4b96bca — discover how to get Century21 Pombal listings  
> **Date:** 2026-06-11  
> **Status:** Research only — no parser implemented

---

## Overview

Century21 Portugal is a Next.js SPA with a REST API backend at `https://www.century21.pt/api/`. The site uses client-side rendering for search results; the API returns full listing data as JSON. All search pages and listing details are served by this API — no hidden XML feeds or third-party backends.

---

## API Details

**Base URL:** `https://www.century21.pt/api`

### 1. Properties List — `GET /api/properties`

The primary endpoint for discovering listings. Returns paginated JSON with full property detail.

**Parameters:**

| Param | Value | Required | Description |
|---|---|---|---|
| `addresses` | `1015` | Yes | Geocode ID for Pombal (district/concelho level) |
| `address_names` | `Pombal` | Yes | Human-readable location name |
| `ad_type` | `sell` or `rent` | Yes | Transaction type |
| `asset_type` | house/apartment/land/urban_land/store/warehouse | No | Filter by property type |
| `limit` | 1–50 | No | Results per page (default: 20, max: 50) |
| `offset` | integer | No | Pagination offset |
| `order_by` | `entered_market_desc` etc. | No | Sort order |

**Response shape (success):**

```json
{
  "data": [
    {
      "id": 793350,
      "price": 395000,
      "asset_type": "house",
      "ad_type": "sell",
      "price_per_sq": 1449.54,
      "title": {
        "pt": "Moradia T3 Térrea com garagem e Jardim",
        "en": "Single-Storey 3-Bedroom Villa with Garage and Garden",
        "es": "...",
        "fr": "..."
      },
      "link": "https://www.century21.pt/ref/0563-01902",
      "reference": "0563-01902",
      "price_hidden": false,
      "gross_area": 272.5,
      "useful_area": 190.99,
      "number_of_rooms": 3,
      "number_of_wcs": 3,
      "number_of_parking_spots": 2,
      "characteristics": ["front_porch", "storage", "barbecue", ...],
      "lat": 39.9463482516022,
      "lng": -8.76565217971802,
      "address": "R. da Charneca 5, 3105-187 Guia, Portugal",
      "country": "pt",
      "images": ["https://images.century21.pt/<uuid>/101.jpg", ...],
      "entered_market": "2026-06-03T00:00:00.000Z",
      "agent": { "name": "...", "email": "...", "phone": "..." },
      "agency": { "name": "CENTURY 21 ...", "handler": "..." }
    }
  ],
  "total": 164
}
```

**Failure response** (e.g., limit > 50):
```json
{ "success": false, "error": "..." }
```

**Asset type taxonomy:**

| API value | Portuguese | English |
|---|---|---|
| `house` | Moradia | House / Villa |
| `apartment` | Apartamento | Apartment |
| `land` | Terreno | Land |
| `urban_land` | Terreno Urbano | Urban land |
| `store` | Loja | Shop/Store |
| `warehouse` | Armazém | Warehouse |

**Address hierarchy from API:**

| Field | Meaning | Value for Pombal |
|---|---|---|
| `ad_1_id` | Country | `10` (Portugal) |
| `ad_2_id` | Concelho/District | `1015` (Pombal) |
| `ad_3_id` | Freguesia/Parish | `101509` (Pombal), `101514` (Vila Cã), etc. |
| `ad_4_id` | Sub-location | granular zone |

### 2. Autocomplete — `GET /api/autocomplete`

Used for location search-as-you-type.

**Request:** `GET /api/autocomplete?q=Pombal&limit=10`

**Response:**
```json
[
  {
    "code": "1015",
    "name": "Pombal",
    "level": 2,
    "type": "geocode"
  },
  {
    "code": "101509",
    "name": "Pombal, Pombal",
    "level": 3,
    "type": "geocode"
  }
]
```

Level 2 = concelho, Level 3 = freguesia (parish).

### 3. Property Density — `GET /api/properties/density`

Returns map-density data for the same `addresses`/`address_names`/`ad_type` params.

**Request:** `GET /api/properties/density?addresses=1015&address_names=Pombal&ad_type=sell`

### 4. Agencies — `GET /api/agencies`

**Request:** `GET /api/agencies?addresses=1015&address_names=Pombal`

**Response:** Returns agency list with name, address, phone, lat/lng.

---

## Search Page URLs

| Purpose | URL |
|---|---|
| Buy in Pombal | `https://www.century21.pt/comprar?addresses=1015&address_names=Pombal&ad_type=sell` |
| Rent in Pombal | `https://www.century21.pt/comprar?addresses=1015&address_names=Pombal&ad_type=rent` |
| Agency in Pombal | `https://www.century21.pt/agencias/alianca` |

---

## Listing Detail Page

- **Pattern:** `https://www.century21.pt/comprar/{reference}`  
  e.g., `https://www.century21.pt/comprar/0563-01902`
- **Short link:** `https://www.century21.pt/ref/{reference}` (redirects to detail)

No separate API detail endpoint — the list API returns all fields needed.

---

## Pombal-Specific Stats

| Metric | Value |
|---|---|
| Sell listings total | **164** |
| Houses for sale | **69** |
| Apartments for sale | see API (filter `asset_type=apartment`) |
| Rent listings | **3** |
| Agencies in Pombal | **1** — CENTURY 21 Aliança (Rua António Varela Pinto, 3, Pombal) |

---

## Example API URLs

1. **All sell listings in Pombal (first page):**
   ```
   GET /api/properties?addresses=1015&address_names=Pombal&ad_type=sell&limit=20&offset=0
   ```

2. **Houses for sale in Pombal:**
   ```
   GET /api/properties?addresses=1015&address_names=Pombal&asset_type=house&ad_type=sell&limit=50
   ```

3. **Rent in Pombal:**
   ```
   GET /api/properties?addresses=1015&address_names=Pombal&ad_type=rent
   ```

4. **Agency detail (CENTURY 21 Aliança):**
   ```
   GET /api/agencies?addresses=1015&address_names=Pombal
   ```

---

## Implementation Notes (for future parser)

- **No auth required** — API is fully public, no cookies, no API key.
- **Rate limiting** — not observed during testing, but standard practices apply.
- **Image CDN:** `https://images.century21.pt/<uuid>/<filename>` — UUID-based, publicly accessible.
- **Pagination:** Loop with `offset` in increments of `limit` until `offset >= total`.
- **Filtering by parish:** Pass an `ad_3_id` value as `addresses` to scope to a specific freguesia (e.g., `addresses=101514` for Vila Cã).
- **Refresh frequency:** The `entered_market` timestamps suggest near-real-time data from the MLS.
- **Agency codes are included in references** — e.g., `C0381` prefix = CENTURY 21 Aliança (Pombal); `0563` = CENTURY 21 Cardeira e Costa; `0739` = CENTURY 21 P.M. Paiva & Associados.
- **Website search page** reads from the same API via client-side fetch — no SSR.
