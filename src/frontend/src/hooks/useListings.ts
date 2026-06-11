import { useQuery } from '@tanstack/react-query'
import apiClient from '@/lib/api'
import type { PropertyListResponse, ListingsParams } from '@/types/api'

export function useListings(params: ListingsParams = {}) {
  return useQuery({
    queryKey: ['listings', params],
    queryFn: async () => {
      const { data } = await apiClient.get<PropertyListResponse>('/listings', {
        params,
      })
      return data
    },
  })
}
