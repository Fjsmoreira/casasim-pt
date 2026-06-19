import { Home, MapPin, Bed, CalendarDays, Building2 } from 'lucide-react'
import { cn, formatPricePerM2 } from '@/lib/utils'
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
    <article className="group flex overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm transition-all duration-200 hover:-translate-y-0.5 hover:shadow-md">
      {/* Image */}
      <div className="relative h-40 w-36 shrink-0 overflow-hidden bg-gray-100 sm:h-52 sm:w-72">
        {imageUrl ? (
          <img
            src={imageUrl}
            alt={title}
            className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
          />
        ) : (
          <div className="flex h-full w-full items-center justify-center bg-gradient-to-br from-emerald-100 to-emerald-200">
            <Home className="w-12 h-12 text-emerald-400" />
          </div>
        )}
      </div>

      {/* Content */}
      <div className="min-w-0 flex-1 p-4 sm:p-5">
        {/* Price */}
        <p className="text-xl font-bold text-emerald-700">
          €{price.toLocaleString('pt-PT')}
          {transaction === 'rent' && (
            <span className="text-sm font-normal text-gray-500">/mês</span>
          )}
        </p>
        {pricePerM2 && (
          <p className="text-xs font-medium text-gray-500">{pricePerM2}</p>
        )}

        {/* Title */}
        <h3 className="mt-1 font-semibold text-gray-900 line-clamp-1 leading-tight">
          {title}
        </h3>

        {/* Location */}
        <p className="text-sm text-gray-500 truncate flex items-center gap-1">
          <MapPin className="w-3.5 h-3.5 shrink-0" />
          <span>{location}</span>
        </p>

        {/* Badges */}
        <div className="mt-3 flex flex-wrap gap-1.5">
          {areaM2 && (
            <span className="inline-flex items-center gap-1 px-2.5 py-1 bg-gray-100 text-gray-700 text-xs font-medium rounded-md">
              {areaM2}m²
            </span>
          )}
          {bedrooms && (
            <span className="inline-flex items-center gap-1 px-2.5 py-1 bg-gray-100 text-gray-700 text-xs font-medium rounded-md">
              <Bed className="w-3 h-3" />
              {bedrooms} {bedrooms === 1 ? 'quarto' : 'quartos'}
            </span>
          )}
          <span className="inline-flex items-center gap-1 px-2.5 py-1 bg-emerald-50 text-emerald-700 text-xs font-medium rounded-md">
            {typeLabel}
          </span>
          <span
            className={cn(
              'inline-flex items-center gap-1 px-2.5 py-1 text-xs font-medium rounded-md',
              transaction === 'sale'
                ? 'bg-blue-50 text-blue-700'
                : 'bg-purple-50 text-purple-700',
            )}
          >
            {transactionLabel}
          </span>
        </div>

        <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1 border-t border-gray-100 pt-3 text-xs text-gray-500">
          {agency && <span className="inline-flex items-center gap-1"><Building2 className="size-3.5" />{agency.name}</span>}
          {listedDate && <span className="inline-flex items-center gap-1"><CalendarDays className="size-3.5" />Publicado {listedDate}</span>}
          {status && <span className="font-medium text-amber-700">{status}</span>}
        </div>
      </div>
    </article>
  )
}
