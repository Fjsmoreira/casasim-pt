// ─── DTO types for the CasaSim.pt API ──────────────────────────
// All endpoints are proxied via Vite: /api/* → localhost:5000

/** A single property listing (from GET /api/listings list) */
export interface ListingSummary {
  id: string
  title: string
  price: number
  type: 'house' | 'apartment' | 'land' | 'commercial' | 'other'
  propertyType?: string
  transaction: 'sale' | 'rent'
  priceType?: string
  bedrooms?: number
  bathrooms?: number
  areaM2?: number
  landAreaM2?: number
  city?: string
  parish?: string
  district?: string
  status?: string
  primaryImage?: {
    url: string
    thumbnailUrl?: string
    altText?: string
  }
  images?: Array<{
    url: string
    thumbnailUrl?: string
    altText?: string
  }>
  agency?: {
    id: string
    name: string
    slug: string
    websiteUrl?: string
    contactEmail?: string
    contactPhone?: string
  }
  listingUrl?: string
  source?: string
  latitude?: number
  longitude?: number
  publishedAt?: string
  firstSeenAt?: string
  lastSeenAt?: string
  createdAt?: string
  updatedAt?: string
}

export interface Agency {
  id: string
  name: string
  slug: string
  websiteUrl?: string
  contactEmail?: string
  contactPhone?: string
}

/** Full property detail (from GET /api/listings/:id) */
export interface ListingDetail {
  id: string
  title: string
  price: number
  type: 'house' | 'apartment' | 'land' | 'commercial' | 'other'
  propertyType?: string
  transaction: 'sale' | 'rent'
  priceType?: string
  bedrooms?: number
  bathrooms?: number
  areaM2?: number
  landAreaM2?: number
  city?: string
  parish?: string
  district?: string
  description?: string
  images: string[]
  features?: string[]
  sourceUrl?: string
  externalId?: string
  agencyName?: string
  agencyPhone?: string
  agencyEmail?: string
  latitude?: number
  longitude?: number
  publishedAt?: string
  firstSeenAt?: string
  lastSeenAt?: string
  createdAt?: string
  updatedAt?: string
}

/** Search / filter parameters sent to GET /api/listings */
export interface ListingsParams {
  page?: number
  pageSize?: number
  search?: string
  type?: string
  transaction?: string
  minPrice?: number
  maxPrice?: number
  minBedrooms?: number
  minAreaM2?: number
  city?: string
  locality?: string
  agencySlug?: string
  sortBy?: string
  sortDirection?: string
}

/** Response from GET /api/listings (paginated listing) */
export interface PropertyListResponse {
  items: ListingSummary[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

/** Response from GET /api/listings/:id */
export type PropertyDetailResponse = ListingDetail

/** Admin: single listing row in the management table */
export interface AdminListing {
  id: string
  title: string
  price: number | null
  priceFormatted: string | null
  currency: string
  propertyType: string
  status: string
  city: string | null
  bedrooms: number | null
  areaM2: number | null
  thumbnailUrl: string | null
  agencyName: string | null
  agencySlug: string | null
  lastSeenAt: string
  createdAt: string
  updatedAt: string
}

/** Admin: paginated listings response */
export interface AdminListingsResponse {
  items: AdminListing[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

/** Admin: agency record for filter dropdown */
export interface AdminAgency {
  id: string
  name: string
  slug: string
}

/** GeoJSON FeatureCollection for the map page */
export interface MapPropertiesResponse {
  type: 'FeatureCollection'
  features: Array<{
    type: 'Feature'
    geometry: {
      type: 'Point'
      coordinates: [number, number]
    }
    properties: {
      id: string
      price: number
      price_type: string
      currency: string
      property_type: string
      status: string
      city?: string
      bedrooms?: number
      locality?: string
      thumbnail?: string
    }
  }>
}

// ─── Admin: Scraper Status ────────────────────────────────

/** A single scraper source's latest run info */
export interface ScraperSourceRun {
  sourceName: string
  status: 'Started' | 'Succeeded' | 'PartiallySucceeded' | 'Failed' | 'Cancelled'
  startedAt: string | null
  completedAt: string | null
  listingsFound: number
  listingsCreated: number
  listingsUpdated: number
  listingsRemoved: number
  errorMessage: string | null
}

/** A recent scraper error entry */
export interface ScraperError {
  sourceName: string
  startedAt: string | null
  errorMessage: string | null
  errorDetails: string | null
}

/** Response from GET /api/admin/scraper-status */
export interface ScraperStatusResponse {
  sources: ScraperSourceRun[]
  runCounts: Record<string, number>
  recentErrors: ScraperError[]
  lastRunOverall: string | null
}

export interface AdminScraperSource {
  id: string
  name: string
  scraperKey: string
  agencySlug: string
  agencyName: string | null
  sourceUrl: string | null
  targetDescription: string | null
  enabled: boolean
  interval: string
  manualRunRequestedAt: string | null
  updatedAt: string
  latestRun: ScraperRunSummary | null
}

export interface ScraperRunSummary {
  id: string
  sourceName: string
  sourceUrl: string | null
  agencyName: string | null
  agencySlug: string | null
  status: 'Started' | 'Succeeded' | 'PartiallySucceeded' | 'Failed' | 'Cancelled'
  startedAt: string
  completedAt: string | null
  durationSeconds: number | null
  listingsFound: number
  listingsCreated: number
  listingsUpdated: number
  listingsRemoved: number
  errorMessage: string | null
  currentPhase: string | null
  lastActivityAt: string | null
}

export interface ScraperRunDetail extends ScraperRunSummary {
  sourceTargetDescription: string | null
  errorDetails: string | null
}

export interface ScrapeRunActivity {
  id: string
  level: 'Information' | 'Warning' | 'Error'
  phase: string
  message: string
  currentCount: number | null
  totalCount: number | null
  createdAt: string
}

export interface ScrapeRunActivityResponse {
  items: ScrapeRunActivity[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface ScrapeListingChange {
  id: string
  scrapeLogId: string
  listingId: string | null
  action: 'Created' | 'Updated' | 'Removed' | 'Skipped'
  agencySlug: string
  externalId: string
  title: string | null
  sourceUrl: string | null
  changeSummaryJson: string | null
  createdAt: string
}

export interface ScraperRunsResponse {
  items: ScraperRunSummary[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface ScrapeListingChangesResponse {
  items: ScrapeListingChange[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}
