# Remax Test Fixtures

## Overview

HTML and JSON fixtures from real Remax Portugal listing pages for parser unit tests.
All fixtures obtained from publicly accessible URLs (sitemap + API), fetched with standard
User-Agent headers, respecting robots.txt and rate limits (single fetch, no pagination).

## Files

### HTML Fixtures (source: live Remax detail pages)
| File | Type | Public ID | Description |
|------|------|-----------|-------------|
| `remax_moradia_t2_abiul.html` | Venda - Moradia T2 | 122591135-5 | Stone house, 78m² living, 1,290m² lot, Abiul/Pombal |
| `remax_terreno_santiago.html` | Venda - Terreno | 122591077-461 | Land plot in Santiago e São Simão de Litém, Pombal |
| `remax_apartamento_t2_pombal.html` | Arrendamento - Apartamento T2 | 124631157-21 | Rental apartment, 55m², Pombal city center |

Each HTML file contains full server-rendered Next.js page with `__NEXT_DATA__`
script tag embedding the `listingEncoded` (base64-encoded JSON with all listing fields:
price, type, bedrooms, bathrooms, area, coordinates, images, descriptions, features, agent info).

### JSON Fixtures (source: Remax detail API)
| File | Public ID | Notes |
|------|-----------|-------|
| `remax_api_detail_moradia_122591135-5.json` | 122591135-5 | Full API response |
| `remax_api_detail_terreno_122591077-461.json` | 122591077-461 | Full API response |
| `remax_api_detail_apartamento_124631157-21.json` | 124631157-21 | Full API response |

API endpoint: `GET /api/Listing/GetListingByTitle?listingPublicId={publicId}`
Base URL: `https://api-v2-prod-remaxpt.devscope.net`

## Coverage

- **3 property types**: House (Moradia), Land (Terreno), Apartment (Apartamento)
- **2 transaction types**: Sale (Venda) and Rent (Arrendamento)
- **3 parishes**: Abiul, Santiago e São Simão de Litém e Albergaria dos Doze, Pombal
- **Price range**: varies (land: €25k–€250k, house: €30k, rent: varies)
- **Images**: 4–26 pictures per listing

## Usage

In xUnit tests, load fixtures with:

```csharp
var html = await File.ReadAllTextAsync("Fixtures/remax_moradia_t2_abiul.html");
```

Ensure the .csproj includes:

```xml
<ItemGroup>
  <Content Include="Fixtures/**/*" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

## Source reliability

- **HTML fixtures**: Server-rendered detail pages from `remax.pt/pt/imoveis/*`
  (Next.js SSR with embedded `__NEXT_DATA__`). No JS execution needed.
- **JSON fixtures**: From the public Remax API (`api-v2-prod-remaxpt.devscope.net`),
  no authentication required. API does not appear to block standard requests.
- **Robots**: Sitemap XMLs are explicitly intended for crawlers.
  Single requests only — no batching, no pagination.
