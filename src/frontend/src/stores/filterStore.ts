import { create } from 'zustand'
import type { ListingsParams } from '@/types/api'

export interface FilterState {
  priceMin: number | undefined
  priceMax: number | undefined
  type: string | undefined
  bedrooms: number | undefined
  minAreaM2: number | undefined
  transaction: string | undefined
  locality: string | undefined
  agencySlug: string | undefined
  sortBy: string | undefined
  sortDirection: 'Asc' | 'Desc' | undefined
  mobileOpen: boolean

  setPriceMin: (val: number | undefined) => void
  setPriceMax: (val: number | undefined) => void
  setType: (val: string | undefined) => void
  setBedrooms: (val: number | undefined) => void
  setMinAreaM2: (val: number | undefined) => void
  setTransaction: (val: string | undefined) => void
  setLocality: (val: string | undefined) => void
  setAgencySlug: (val: string | undefined) => void
  setSortBy: (val: string | undefined) => void
  setSortDirection: (val: 'Asc' | 'Desc' | undefined) => void
  hydrate: (filters: Partial<Pick<FilterState, 'priceMin' | 'priceMax' | 'type' | 'bedrooms' | 'minAreaM2' | 'transaction' | 'locality' | 'agencySlug' | 'sortBy' | 'sortDirection'>>) => void
  setMobileOpen: (val: boolean) => void
  clearFilters: () => void
  toParams: () => ListingsParams
}

const initialState = {
  priceMin: undefined,
  priceMax: undefined,
  type: 'house,apartment',
  bedrooms: undefined,
  minAreaM2: undefined,
  transaction: 'sale',
  locality: undefined,
  agencySlug: undefined,
  sortBy: undefined,
  sortDirection: undefined,
}

export const useFilterStore = create<FilterState>((set, get) => ({
  ...initialState,
  mobileOpen: false,

  setPriceMin: (val) => set({ priceMin: val }),
  setPriceMax: (val) => set({ priceMax: val }),
  setType: (val) => set({ type: val }),
  setBedrooms: (val) => set({ bedrooms: val }),
  setMinAreaM2: (val) => set({ minAreaM2: val }),
  setTransaction: (val) => set({ transaction: val }),
  setLocality: (val) => set({ locality: val }),
  setAgencySlug: (val) => set({ agencySlug: val }),
  setSortBy: (val) => set({ sortBy: val }),
  setSortDirection: (val) => set({ sortDirection: val }),
  hydrate: (filters) => set({ ...initialState, ...filters }),
  setMobileOpen: (val) => set({ mobileOpen: val }),

  clearFilters: () => set({ ...initialState }),

  toParams: () => {
    const { priceMin, priceMax, type, bedrooms, minAreaM2, transaction, locality, agencySlug, sortBy, sortDirection } = get()
    const params: ListingsParams = {}
    if (priceMin !== undefined) params.minPrice = priceMin
    if (priceMax !== undefined) params.maxPrice = priceMax
    if (type) params.type = type
    if (bedrooms !== undefined) params.minBedrooms = bedrooms
    if (minAreaM2 !== undefined) params.minAreaM2 = minAreaM2
    if (transaction) params.transaction = transaction
    if (locality) params.locality = locality
    if (agencySlug) params.agencySlug = agencySlug
    if (sortBy) {
      params.sortBy = sortBy
      params.sortDirection = sortDirection ?? 'Desc'
    }
    return params
  },
}))
