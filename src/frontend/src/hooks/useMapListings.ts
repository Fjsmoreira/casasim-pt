import { useQuery } from '@tanstack/react-query'
import apiClient from '@/lib/api'
import type { MapPropertiesResponse } from '@/types/api'

export function useMapListings() {
  return useQuery({
    queryKey: ['map-listings'],
    queryFn: async () => {
      const { data } = await apiClient.get<MapPropertiesResponse>(
        '/properties/map',
      )
      return data
    },
  })
}
