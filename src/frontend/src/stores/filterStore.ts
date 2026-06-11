import { create } from 'zustand'
import type { ListingsParams } from '@/types/api'

export interface FilterState {
  priceMin: number | undefined
  priceMax: number | undefined
  type: string | undefined
  bedrooms: number | undefined
  transaction: string | undefined
  mobileOpen: boolean

  setPriceMin: (val: number | undefined) => void
  setPriceMax: (val: number | undefined) => void
  setType: (val: string | undefined) => void
  setBedrooms: (val: number | undefined) => void
  setTransaction: (val: string | undefined) => void
  setMobileOpen: (val: boolean) => void
  clearFilters: () => void
  toParams: () => ListingsParams
}

const initialState = {
  priceMin: undefined,
  priceMax: undefined,
  type: undefined,
  bedrooms: undefined,
  transaction: undefined,
}

export const useFilterStore = create<FilterState>((set, get) => ({
  ...initialState,
  mobileOpen: false,

  setPriceMin: (val) => set({ priceMin: val }),
  setPriceMax: (val) => set({ priceMax: val }),
  setType: (val) => set({ type: val }),
  setBedrooms: (val) => set({ bedrooms: val }),
  setTransaction: (val) => set({ transaction: val }),
  setMobileOpen: (val) => set({ mobileOpen: val }),

  clearFilters: () => set({ ...initialState }),

  toParams: () => {
    const { priceMin, priceMax, type, bedrooms, transaction } = get()
    const params: ListingsParams = {}
    if (priceMin !== undefined) params.minPrice = priceMin
    if (priceMax !== undefined) params.maxPrice = priceMax
    if (type) params.type = type
    if (bedrooms !== undefined) params.minBedrooms = bedrooms
    if (transaction) params.transaction = transaction
    return params
  },
}))
