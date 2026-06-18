# CasaSim App Kanban

Updated: 2026-06-18

This board tracks app-building work only. DNS and SEO are intentionally out of scope for the current focus.

## In Progress

| ID | Priority | Area | Task | Acceptance criteria | Test plan | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| APP-001 | High | scraper | Remax sitemap discovery validation | Remax discovery reads the sitemap index, scans listing-detail sitemaps, filters Pombal listing URLs, deduplicates URLs, and skips non-Pombal URLs. | Add fake-HTTP scraper tests for sitemap index, listing sitemap, duplicate URLs, and detail fetch behavior. | First slice of the Remax completion workstream. |

## Ready

| ID | Priority | Area | Task | Acceptance criteria | Test plan | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| APP-002 | High | scraper | Remax detail parsing hardening | Parser handles the current Remax detail shape and maps ID, URL, title, price, type, transaction, area, location, and images. | Extend parser fixture coverage only if the current live shape differs from existing fixtures. | Existing parser coverage is already broad; avoid rewriting. |
| APP-003 | High | scraper/backend | Remax DB upsert verification | A Remax scrape creates listings once and updates them on repeated runs without duplicates. | Add/confirm upsert tests for create/update/skipped behavior using Remax properties. Do one controlled staging/live run. | Depends on APP-001 and APP-002. |
| APP-004 | High | scraper/ops | Remax deploy and monitor readiness | Admin scraper status shows Remax run outcome, and one failed detail page does not fail the whole source. | Fake-HTTP test for partial detail failure; production smoke check through admin scraper status. | Deploy only after DB upsert is verified. |
| APP-006 | Medium | frontend | New listing badge | Listings first seen in the last 7 days show a "New" badge in list/card views. | Date-boundary component tests. | Requires a suitable created/first-seen field in the API DTO. |
| APP-007 | Medium | frontend | Favorites | Users can add/remove favorites in localStorage from cards and detail pages, and revisit saved listings. | Tests for add, remove, persisted state, and missing listing behavior. | No auth for v1. |
| APP-008 | Medium | frontend | Empty states | Search and map/list views show useful empty states when filters return zero listings. | Search page test with zero-result API response. | Keep copy concise and practical. |

## Blocked

| ID | Priority | Area | Task | Blocker | Next action |
| --- | --- | --- | --- | --- | --- |
| APP-009 | Medium | backend | Rate limiting for public listing endpoints | Waiting until Remax and first UX wins are complete. | Apply ASP.NET rate-limit policy to public listing endpoints. |
| APP-010 | Medium | backend | Price history tracking | Needs schema/API decision after Remax upsert path is stable. | Design minimal price-history table and upsert behavior. |
| APP-011 | Low | backend/data | Duplicate detection research spike | Needs more cross-agency inventory after Remax. | Document candidate matching rules with sample duplicates. |

## Done

| ID | Priority | Area | Task | Result |
| --- | --- | --- | --- | --- |
| APP-005 | Medium | frontend | Price per m2 | Implemented on listing cards and detail headers; frontend tests, lint, and build pass. |

## Later

| ID | Priority | Area | Task | Notes |
| --- | --- | --- | --- | --- |
| APP-012 | High | scraper | Idealista scraper research | Start only after Remax is deployed and monitored. |
| APP-013 | High | scraper | Imovirtual scraper research | Start only after Remax is deployed and monitored. |
| APP-014 | Medium | scraper | SuperCasa scraper research | Lower priority than Idealista and Imovirtual. |
| APP-015 | Medium | scraper | Zome scraper research | Lower priority than Idealista and Imovirtual. |
| APP-016 | Medium | scraper | KW Portugal scraper research | Lower priority than Idealista and Imovirtual. |
| APP-017 | Low | scraper | OLX Imoveis scraper research | Private-seller inventory may be valuable but harder to normalize. |
