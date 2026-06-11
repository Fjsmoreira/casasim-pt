import type { Property, PropertyListResponse, MapPropertiesResponse } from '@/types/api'

export const mockProperty: Property = {
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
  images: ['https://example.com/photo.jpg'],
  features: ['Lareira', 'Garagem'],
  listingUrl: 'https://example.com/listing/1',
  latitude: 39.915,
  longitude: -8.628,
  createdAt: '2025-01-15T10:00:00Z',
  source: 'idealista',
  sourceId: 'src-1',
  agencyName: 'Imobiliária Central',
  agencyLogo: undefined,
  agencyPhone: '+351 123 456 789',
  agencyEmail: 'info@central.pt',
}

export const mockPropertyNoImage: Property = {
  ...mockProperty,
  id: 'prop-2',
  title: 'Apartamento T2 na Vila',
  price: 95000,
  type: 'apartment',
  transaction: 'sale',
  bedrooms: 2,
  areaM2: 85,
  images: [],
  city: 'Pombal',
  parish: 'Vila',
}

export const mockPropertyRent: Property = {
  ...mockProperty,
  id: 'prop-3',
  title: 'Apartamento T1 para Arrendar',
  price: 650,
  type: 'apartment',
  transaction: 'rent',
  bedrooms: 1,
  areaM2: 55,
  images: ['https://example.com/rent.jpg'],
  city: 'Leiria',
  parish: 'Leiria',
}

export const mockListResponse: PropertyListResponse = {
  items: [mockProperty, mockPropertyNoImage, mockPropertyRent],
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
