import { http, HttpResponse } from 'msw'
import { mockListResponse, mockEmptyListResponse, mockListingDetail, mockGeoJson, mockEmptyGeoJson } from './data'

export const handlers = [
  http.get('/api/agencies', () => HttpResponse.json([
    { id: 'agency-1', name: 'Imobiliária Central', slug: 'imobiliaria-central' },
    { id: 'agency-2', name: 'Zome', slug: 'zome' },
  ])),

  // GET /api/listings — listings (SearchPage)
  http.get('/api/listings', ({ request }) => {
    const url = new URL(request.url)
    const page = url.searchParams.get('page')

    // Simulate error when specific params are passed
    if (url.searchParams.get('error') === 'true') {
      return HttpResponse.json(
        { message: 'Erro ao carregar imóveis' },
        { status: 500 },
      )
    }

    // Return empty for page 99 (used by empty state test)
    if (page === '99') {
      return HttpResponse.json(mockEmptyListResponse)
    }

    return HttpResponse.json(mockListResponse)
  }),

  // GET /api/listings/:id — single listing detail
  http.get('/api/listings/:id', ({ params }) => {
    const { id } = params

    if (id === '404-test') {
      return HttpResponse.json(
        { message: 'Property not found' },
        { status: 404 },
      )
    }

    if (id === 'error-test') {
      return HttpResponse.json(
        { message: 'Erro ao carregar imóvel' },
        { status: 500 },
      )
    }

    return HttpResponse.json(mockListingDetail)
  }),

  // GET /api/listings/geojson — map page
  http.get('/api/listings/geojson', ({ request }) => {
    const url = new URL(request.url)
    const swLat = url.searchParams.get('swLat')

    // Return empty GeoJSON when given a specific query
    if (swLat === '0') {
      return HttpResponse.json(mockEmptyGeoJson)
    }

    return HttpResponse.json(mockGeoJson)
  }),
]
