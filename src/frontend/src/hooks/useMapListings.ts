import { useQuery } from '@tanstack/react-query'
import apiClient from '@/lib/api'
import type { ListingsParams, MapPropertiesResponse } from '@/types/api'

export function useMapListings(params: ListingsParams = {}) {
  return useQuery({
    queryKey: ['map-listings', params],
    queryFn: async () => {
      const { data } = await apiClient.get<MapPropertiesResponse>(
        '/listings/geojson',
        { params: {
          swLat: 38, swLng: -9.5, neLat: 42, neLng: -6,
          type: params.type,
          priceType: params.transaction,
          minPrice: params.minPrice,
          maxPrice: params.maxPrice,
          minBedrooms: params.minBedrooms,
          locality: params.locality,
        } },
      )
      return data
    },
    staleTime: 5 * 60 * 1000,
  })
}
