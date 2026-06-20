import { useQuery } from '@tanstack/react-query'
import apiClient from '@/lib/api'
import type { Agency } from '@/types/api'

export function useAgencies() {
  return useQuery({
    queryKey: ['agencies'],
    queryFn: async () => {
      const { data } = await apiClient.get<Agency[]>('/agencies')
      return data
    },
  })
}
