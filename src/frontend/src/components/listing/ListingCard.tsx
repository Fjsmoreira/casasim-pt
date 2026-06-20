import { Bath, BedDouble, CalendarDays, Heart, Home, MapPin, Maximize, Building2 } from 'lucide-react'
import { formatPricePerM2 } from '@/lib/utils'
import type { ListingSummary } from '@/types/api'

const TYPE_LABELS: Record<string, string> = {
  house: 'Casa',
  apartment: 'Apartamento',
  land: 'Terreno',
  commercial: 'Comercial',
  other: 'Outro',
}

const TRANSACTION_LABELS: Record<string, string> = {
  sale: 'Venda',
  rent: 'Arrendamento',
}

function normalizeKey(value?: string) {
  return value ? value.charAt(0).toLowerCase() + value.slice(1) : undefined
}

function formatListingDate(value?: string) {
  if (!value) return null
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return null
  return new Intl.DateTimeFormat('pt-PT', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  }).format(date)
}

export interface ListingCardProps {
  property: ListingSummary
}

export default function ListingCard({ property }: ListingCardProps) {
  const { title, price, bedrooms, areaM2, city, parish, primaryImage, agency } =
    property

  const type = normalizeKey(property.type ?? property.propertyType) ?? 'other'
  const transaction = normalizeKey(property.transaction ?? property.priceType) ?? 'sale'
  const imageUrl = primaryImage?.thumbnailUrl || primaryImage?.url
  const location = [parish, city].filter(Boolean).join(', ') || 'Pombal'
  const typeLabel = TYPE_LABELS[type] || type
  const transactionLabel = TRANSACTION_LABELS[transaction] || transaction
  const pricePerM2 = formatPricePerM2(price, areaM2, transaction === 'rent' ? 'rent' : 'sale')
  const listedDate = formatListingDate(property.publishedAt ?? property.firstSeenAt ?? property.createdAt)
  const status = property.status && property.status.toLowerCase() !== 'active' ? property.status : null

  return (
    <article className="group overflow-hidden rounded border border-slate-200 bg-white shadow-sm transition-all duration-200 hover:border-slate-300 hover:shadow-md">
      {/* Image */}
      <div className="relative h-56 overflow-hidden bg-slate-100 sm:h-72">
        {imageUrl ? (
          <img
            src={imageUrl}
            alt={title}
            className="h-full w-full object-cover transition-transform duration-500 group-hover:scale-[1.03]"
          />
        ) : (
          <div className="flex h-full w-full items-center justify-center bg-gradient-to-br from-sky-100 to-sky-200">
            <Home className="size-12 text-sky-400" />
          </div>
        )}
        <span className="absolute left-3 top-3 rounded bg-[#f7a21b] px-2 py-1 text-[11px] font-bold uppercase tracking-wide text-white">{transactionLabel}</span>
        {status && <span className="absolute left-3 top-11 rounded bg-slate-800/85 px-2 py-1 text-[11px] font-semibold text-white">{status}</span>}
        <button type="button" aria-label="Adicionar aos favoritos" onClick={(event) => event.preventDefault()} className="absolute right-3 top-3 grid size-9 place-items-center rounded-full bg-white/95 text-sky-800 shadow-sm transition hover:bg-white"><Heart className="size-5" /></button>
      </div>

      {/* Content */}
      <div className="min-w-0 p-4 sm:p-5">
        <div className="flex items-start justify-between gap-4">
          <div className="min-w-0"><h3 className="truncate text-base font-bold text-sky-800 group-hover:underline">{title}</h3><p className="mt-0.5 flex items-center gap-1 text-sm text-slate-600"><MapPin className="size-3.5 shrink-0" /><span className="truncate">{location}</span></p></div>
          <p className="shrink-0 text-xl font-bold text-slate-900">
          €{price.toLocaleString('pt-PT')}
          {transaction === 'rent' && (
            <span className="text-sm font-normal text-gray-500">/mês</span>
          )}
          </p>
        </div>
        {pricePerM2 && (
          <p className="mt-1 text-xs font-medium text-slate-500">{pricePerM2}</p>
        )}
        <div className="mt-4 flex flex-wrap gap-x-5 gap-y-2 border-y border-slate-100 py-3 text-sm font-medium text-slate-700">
          {areaM2 && (
            <span className="inline-flex items-center gap-1"><Maximize className="size-4 text-slate-500" />
              {areaM2}m²
            </span>
          )}
          {bedrooms && (
            <span className="inline-flex items-center gap-1"><BedDouble className="size-4 text-slate-500" />
              {bedrooms} {bedrooms === 1 ? 'quarto' : 'quartos'}
            </span>
          )}
          {property.bathrooms && <span className="inline-flex items-center gap-1"><Bath className="size-4 text-slate-500" />{property.bathrooms} {property.bathrooms === 1 ? 'casa de banho' : 'casas de banho'}</span>}
          <span className="text-slate-500">{typeLabel}</span>
        </div>

        <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-slate-500">
          {agency && <span className="inline-flex items-center gap-1"><Building2 className="size-3.5" />{agency.name}</span>}
          {listedDate && <span className="inline-flex items-center gap-1"><CalendarDays className="size-3.5" />Publicado {listedDate}</span>}
        </div>
      </div>
    </article>
  )
}
