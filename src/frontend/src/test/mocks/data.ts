import type { ListingSummary, ListingDetail, PropertyListResponse, MapPropertiesResponse } from '@/types/api'

export const mockListing: ListingSummary = {
  id: 'prop-1',
  title: 'Moradia T3 em Pombal',
  price: 185000,
  type: 'house',
  transaction: 'sale',
  bedrooms: 3,
  bathrooms: 2,
  areaM2: 150,
  landAreaM2: 500,
  city: 'Pombal',
  parish: 'Pombal',
  district: 'Leiria',
  primaryImage: {
    url: 'https://example.com/photo.jpg',
    thumbnailUrl: 'https://example.com/photo_thumb.jpg',
    altText: 'Moradia T3 em Pombal',
  },
  images: [
    { url: 'https://example.com/photo.jpg', thumbnailUrl: 'https://example.com/photo_thumb.jpg', altText: 'Moradia T3 em Pombal' },
    { url: 'https://example.com/photo2.jpg', thumbnailUrl: 'https://example.com/photo2_thumb.jpg', altText: 'Moradia T3 em Pombal — exterior' },
    { url: 'https://example.com/photo3.jpg', thumbnailUrl: 'https://example.com/photo3_thumb.jpg', altText: 'Moradia T3 em Pombal — jardim' },
  ],
  agency: {
    id: 'agency-1',
    name: 'Imobiliária Central',
    slug: 'imobiliaria-central',
    websiteUrl: 'https://central.pt',
    contactEmail: 'info@central.pt',
    contactPhone: '+351 123 456 789',
  },
  listingUrl: 'https://example.com/listing/1',
  source: 'idealista',
  latitude: 39.915,
  longitude: -8.628,
  createdAt: '2025-01-15T10:00:00Z',
}

export const mockListingNoImage: ListingSummary = {
  ...mockListing,
  id: 'prop-2',
  title: 'Apartamento T2 na Vila',
  price: 95000,
  type: 'apartment',
  transaction: 'sale',
  bedrooms: 2,
  areaM2: 85,
  primaryImage: undefined,
  images: undefined,
  city: 'Pombal',
  parish: 'Vila',
}

export const mockListingRent: ListingSummary = {
  ...mockListing,
  id: 'prop-3',
  title: 'Apartamento T1 para Arrendar',
  price: 650,
  type: 'apartment',
  transaction: 'rent',
  bedrooms: 1,
  areaM2: 55,
  primaryImage: {
    url: 'https://example.com/rent.jpg',
    thumbnailUrl: 'https://example.com/rent_thumb.jpg',
  },
  images: [
    { url: 'https://example.com/rent.jpg', thumbnailUrl: 'https://example.com/rent_thumb.jpg' },
  ],
  city: 'Leiria',
  parish: 'Leiria',
}

export const mockListingDetail: ListingDetail = {
  id: 'prop-1',
  title: 'Moradia T3 em Pombal',
  price: 185000,
  type: 'house',
  transaction: 'sale',
  bedrooms: 3,
  bathrooms: 2,
  areaM2: 150,
  landAreaM2: 500,
  city: 'Pombal',
  parish: 'Pombal',
  district: 'Leiria',
  description: 'Linda moradia em Pombal com excelentes acabamentos.',
  images: ['https://example.com/photo.jpg', 'https://example.com/photo2.jpg'],
  features: ['Lareira', 'Garagem'],
  sourceUrl: 'https://example.com/listing/1',
  externalId: 'src-1',
  agencyName: 'Imobiliária Central',
  agencyPhone: '+351 123 456 789',
  agencyEmail: 'info@central.pt',
  latitude: 39.915,
  longitude: -8.628,
  createdAt: '2025-01-15T10:00:00Z',
}

export const mockListResponse: PropertyListResponse = {
  items: [mockListing, mockListingNoImage, mockListingRent],
  page: 1,
  pageSize: 12,
  totalCount: 3,
  totalPages: 1,
}

export const mockEmptyListResponse: PropertyListResponse = {
  items: [],
  page: 1,
  pageSize: 12,
  totalCount: 0,
  totalPages: 1,
}

export const mockGeoJson: MapPropertiesResponse = {
  type: 'FeatureCollection',
  features: [
    {
      type: 'Feature',
      geometry: { type: 'Point', coordinates: [-8.628, 39.915] },
      properties: {
        id: 'prop-1',
        price: 185000,
        price_type: 'Sale',
        currency: 'EUR',
        property_type: 'House',
        status: 'Active',
        city: 'Pombal',
        bedrooms: 3,
        thumbnail: 'https://example.com/thumb.jpg',
      },
    },
  ],
}

export const mockEmptyGeoJson: MapPropertiesResponse = {
  type: 'FeatureCollection',
  features: [],
}
