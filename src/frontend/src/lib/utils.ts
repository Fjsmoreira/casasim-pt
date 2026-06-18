import { type ClassValue, clsx } from 'clsx'
import { twMerge } from 'tailwind-merge'

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function formatPricePerM2(
  price?: number | null,
  areaM2?: number | null,
  transaction?: 'sale' | 'rent',
) {
  if (!price || !areaM2 || areaM2 <= 0) return null

  const value = Math.round(price / areaM2).toLocaleString('pt-PT')
  return `${value} €/m²${transaction === 'rent' ? '/mês' : ''}`
}
