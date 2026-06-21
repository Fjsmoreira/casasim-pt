import { MapPin, ExternalLink } from 'lucide-react'
import { cn, formatPricePerM2 } from '@/lib/utils'

interface PropertyHeaderProps {
  title: string
  price: number
  transaction: 'sale' | 'rent'
  areaM2?: number
  parish?: string
  city?: string
  district?: string
  listingUrl?: string
  source?: string
}

export default function PropertyHeader({
  title,
  price,
  transaction,
  areaM2,
  parish,
  city,
  district,
  listingUrl,
  source,
}: PropertyHeaderProps) {
  const formatter = new Intl.NumberFormat('pt-PT', {
    style: 'currency',
    currency: 'EUR',
    maximumFractionDigits: 0,
  })

  const locationParts = [parish, city, district].filter(Boolean) as string[]
  const pricePerM2 = formatPricePerM2(price, areaM2, transaction)

  return (
    <div className="mb-8">
      <h1 className="text-2xl sm:text-3xl font-bold text-gray-900 mb-2">
        {title}
      </h1>

      <div className="flex flex-wrap items-center gap-x-3 gap-y-1.5 mb-3">
        <span className="text-2xl font-bold text-sky-700">
          {formatter.format(price)}
        </span>
        <span
          className={cn(
            'inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium',
            transaction === 'sale'
              ? 'bg-blue-100 text-blue-700'
              : 'bg-purple-100 text-purple-700',
          )}
        >
          {transaction === 'sale' ? 'Venda' : 'Arrendamento'}
        </span>
        {pricePerM2 && (
          <span className="text-sm font-medium text-gray-500">
            {pricePerM2}
          </span>
        )}
      </div>

      {/* Location */}
      {locationParts.length > 0 && (
        <div className="flex items-start gap-2 text-gray-600">
          <MapPin className="h-5 w-5 mt-0.5 shrink-0 text-gray-400" />
          <p className="text-sm">{locationParts.join(', ')}</p>
        </div>
      )}

      {/* Original listing link */}
      {listingUrl && (
        <a
          href={listingUrl}
          target="_blank"
          rel="noopener noreferrer"
          className="inline-flex items-center gap-1.5 text-sm text-sky-600 hover:text-sky-700 transition-colors mt-2"
        >
          <ExternalLink className="h-4 w-4" />
          Ver anúncio original{source ? ` (${source})` : ''}
        </a>
      )}
    </div>
  )
}
