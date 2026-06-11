// ─── DTO types for the CasaSim.pt API ──────────────────────────
// All endpoints are proxied via Vite: /api/* → localhost:5000

/** A single property listing */
export interface Property {
  id: string
  title: string
  price: number
  type: 'house' | 'apartment' | 'land' | 'commercial' | 'other'
  transaction: 'sale' | 'rent'
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
  listingUrl?: string
  latitude?: number
  longitude?: number
  createdAt?: string
  updatedAt?: string
  source?: string
  sourceId?: string
  agencyName?: string
  agencyLogo?: string
  agencyPhone?: string
  agencyEmail?: string
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
  city?: string
}

/** Response from GET /api/listings (paginated listing) */
export interface PropertyListResponse {
  items: Property[]
  total: number
  page: number
  pageSize: number
}

/** Response from GET /api/listings/:id */
export type PropertyDetailResponse = Property

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
      coordinates: [number, number] // [lng, lat]
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
