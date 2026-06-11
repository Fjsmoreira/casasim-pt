import { useQuery } from '@tanstack/react-query'
import apiClient from '@/lib/api'
import type { PropertyDetailResponse } from '@/types/api'

export function useListing(id: string) {
  return useQuery({
    queryKey: ['listing', id],
    queryFn: async () => {
      const { data } = await apiClient.get<PropertyDetailResponse>(
        `/properties/${id}`,
      )
      return data
    },
    enabled: !!id,
  })
}
