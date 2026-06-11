import { useQuery } from '@tanstack/react-query'
import apiClient from '@/lib/api'
import type { MapPropertiesResponse } from '@/types/api'

export function useMapListings() {
  return useQuery({
    queryKey: ['map-listings'],
    queryFn: async () => {
      const { data } = await apiClient.get<MapPropertiesResponse>(
        '/listings/geojson',
        { params: { swLat: 38, swLng: -9.5, neLat: 42, neLng: -6 } },
      )
      return data
    },
    staleTime: 5 * 60 * 1000,
  })
}
