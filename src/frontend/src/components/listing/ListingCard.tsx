import { useState } from 'react'
import { Heart, Home, MapPin, Bed } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { Property } from '@/types/api'

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

export interface ListingCardProps {
  property: Property
  onFavoriteToggle?: (id: string) => void
}

export default function ListingCard({ property, onFavoriteToggle }: ListingCardProps) {
  const [isFavorited, setIsFavorited] = useState(false)

  const { id, title, price, type, transaction, bedrooms, areaM2, city, parish, images } =
    property

  const imageUrl = images?.[0]
  const location = [parish, city].filter(Boolean).join(', ') || 'Pombal'
  const typeLabel = TYPE_LABELS[type] || type
  const transactionLabel = TRANSACTION_LABELS[transaction] || transaction

  return (
    <div className="group bg-white rounded-xl border border-gray-200 overflow-hidden shadow-sm hover:shadow-md transition-all duration-200 hover:-translate-y-0.5">
      {/* Image */}
      <div className="relative aspect-[4/3] overflow-hidden bg-gray-100">
        {imageUrl ? (
          <img
            src={imageUrl}
            alt={title}
            className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
          />
        ) : (
          <div className="w-full h-full flex items-center justify-center bg-gradient-to-br from-emerald-100 to-emerald-200">
            <Home className="w-12 h-12 text-emerald-400" />
          </div>
        )}

        {/* Favorite button */}
        <button
          onClick={() => {
            setIsFavorited(!isFavorited)
            onFavoriteToggle?.(id)
          }}
          className={cn(
            'absolute top-3 right-3 p-2 rounded-full bg-white/80 backdrop-blur-sm shadow-sm transition-all duration-200 hover:scale-110',
            isFavorited ? 'text-red-500' : 'text-gray-400 hover:text-red-400',
          )}
          aria-label={
            isFavorited ? 'Remover dos favoritos' : 'Adicionar aos favoritos'
          }
        >
          <Heart className={cn('w-5 h-5', isFavorited && 'fill-current')} />
        </button>
      </div>

      {/* Content */}
      <div className="p-4 space-y-2">
        {/* Price */}
        <p className="text-xl font-bold text-emerald-700">
          €{price.toLocaleString('pt-PT')}
          {transaction === 'rent' && (
            <span className="text-sm font-normal text-gray-500">/mês</span>
          )}
        </p>

        {/* Title */}
        <h3 className="font-semibold text-gray-900 line-clamp-1 leading-tight">
          {title}
        </h3>

        {/* Location */}
        <p className="text-sm text-gray-500 truncate flex items-center gap-1">
          <MapPin className="w-3.5 h-3.5 shrink-0" />
          <span>{location}</span>
        </p>

        {/* Badges */}
        <div className="flex flex-wrap gap-1.5 pt-1">
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
      </div>
    </div>
  )
}
