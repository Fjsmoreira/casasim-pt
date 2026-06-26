import { Bath, BedDouble, CalendarDays, Heart, Home, MapPin, Maximize, Building2 } from 'lucide-react'
import { formatListingPrice, formatPricePerM2 } from '@/lib/utils'
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

const DEAL_LABELS: Record<string, { label: string; className: string }> = {
  GoodDeal: { label: 'Bom negócio', className: 'bg-emerald-50 text-emerald-700 ring-emerald-200' },
  Neutral: { label: 'Neutro', className: 'bg-slate-50 text-slate-600 ring-slate-200' },
  BadDeal: { label: 'Mau negócio', className: 'bg-rose-50 text-rose-700 ring-rose-200' },
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
  const images = property.images?.length
    ? property.images
    : primaryImage ? [primaryImage] : []
  const location = [parish, city].filter(Boolean).join(', ') || 'Pombal'
  const typeLabel = TYPE_LABELS[type] || type
  const transactionLabel = TRANSACTION_LABELS[transaction] || transaction
  const pricePerM2 = formatPricePerM2(price, areaM2, transaction === 'rent' ? 'rent' : 'sale')
  const listedDate = formatListingDate(property.publishedAt ?? property.firstSeenAt ?? property.createdAt)
  const status = property.status && property.status.toLowerCase() !== 'active' ? property.status : null
  const deal = property.ai?.dealLabel ? DEAL_LABELS[property.ai.dealLabel] : null

  return (
    <article className="group overflow-hidden rounded border border-slate-200 bg-white shadow-sm transition-all duration-200 hover:border-slate-300 hover:shadow-md">
      {/* Image */}
      <div className="relative grid h-56 grid-cols-[2fr_1fr] gap-1 overflow-hidden bg-slate-100 sm:h-72">
        {images[0] ? (
          <img
            src={images[0].thumbnailUrl || images[0].url}
            alt={title}
            className="h-full w-full object-cover transition-transform duration-500 group-hover:scale-[1.03]"
          />
        ) : (
          <div className="flex h-full w-full items-center justify-center bg-gradient-to-br from-sky-100 to-sky-200">
            <Home className="size-12 text-sky-400" />
          </div>
        )}
        <div className="grid grid-rows-2 gap-1">
          {[images[1], images[2]].map((image, index) => image ? (
            <img key={image.url} src={image.thumbnailUrl || image.url} alt={image.altText || `${title} — foto ${index + 2}`} className="h-full w-full object-cover" />
          ) : (
            <div key={index} className="flex items-center justify-center bg-slate-200 text-slate-400"><Home className="size-6" /></div>
          ))}
        </div>
        <span className="absolute left-3 top-3 rounded bg-[#4f8fb3] px-2 py-1 text-[11px] font-bold uppercase tracking-wide text-white">{transactionLabel}</span>
        {status && <span className="absolute left-3 top-11 rounded bg-slate-800/85 px-2 py-1 text-[11px] font-semibold text-white">{status}</span>}
        {deal && <span className={`absolute bottom-3 left-3 rounded-full px-2.5 py-1 text-[11px] font-bold ring-1 ${deal.className}`}>{deal.label}</span>}
        <button type="button" aria-label="Adicionar aos favoritos" onClick={(event) => event.preventDefault()} className="absolute right-3 top-3 grid size-9 place-items-center rounded-full bg-white/95 text-sky-800 shadow-sm transition hover:bg-white"><Heart className="size-5" /></button>
      </div>

      {/* Content */}
      <div className="min-w-0 p-4 sm:p-5">
        <div className="flex items-start justify-between gap-4">
          <div className="min-w-0"><h3 className="truncate text-base font-bold text-sky-800 group-hover:underline">{title}</h3><p className="mt-0.5 flex items-center gap-1 text-sm text-slate-600"><MapPin className="size-3.5 shrink-0" /><span className="truncate">{location}</span></p></div>
          <p className="shrink-0 text-xl font-bold text-slate-900">
            {formatListingPrice(price, transaction === 'rent' ? 'rent' : 'sale')}
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
