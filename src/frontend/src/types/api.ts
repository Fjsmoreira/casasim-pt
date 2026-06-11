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

/** Search / filter parameters sent to GET /api/properties */
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

/** Response from GET /api/properties (paginated listing) */
export interface PropertyListResponse {
  items: Property[]
  total: number
  page: number
  pageSize: number
}

/** Response from GET /api/properties/:id */
export type PropertyDetailResponse = Property

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
