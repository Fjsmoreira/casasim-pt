import { useParams, Link } from 'react-router-dom'
import { useListing } from '@/hooks/useListing'
import { ArrowLeft, Loader2, Home, AlertTriangle, MapPin, BedDouble, Bath, Maximize2, LandPlot, Calendar, ExternalLink } from 'lucide-react'
import { cn } from '@/lib/utils'

export default function PropertyDetailPage() {
  const { id } = useParams()
  const { data: listing, isLoading, isError, error } = useListing(id!)

  // ── 404 state ──────────────────────────────────────────────
  if (isError && error && 'response' in error && (error as { response: { status: number } }).response?.status === 404) {
    return (
      <NotFoundPage />
    )
  }

  // ── loading state ─────────────────────────────────────────
  if (isLoading) {
    return (
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="animate-pulse space-y-6">
          {/* Image skeleton */}
          <div className="h-72 sm:h-96 bg-gray-200 rounded-xl" />
          {/* Title skeleton */}
          <div className="space-y-3">
            <div className="h-8 bg-gray-200 rounded w-2/3" />
            <div className="h-6 bg-gray-200 rounded w-1/3" />
          </div>
          {/* Details skeleton */}
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
            {[1, 2, 3, 4].map((i) => (
              <div key={i} className="h-20 bg-gray-200 rounded-lg" />
            ))}
          </div>
          {/* Description skeleton */}
          <div className="space-y-2">
            <div className="h-4 bg-gray-200 rounded w-full" />
            <div className="h-4 bg-gray-200 rounded w-5/6" />
            <div className="h-4 bg-gray-200 rounded w-3/4" />
            <div className="h-4 bg-gray-200 rounded w-2/3" />
          </div>
        </div>

        {/* Screen‑reader live region */}
        <span className="sr-only" role="status">A carregar detalhes do imóvel...</span>
      </div>
    )
  }

  // ── generic error state ───────────────────────────────────
  if (isError) {
    return (
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="flex flex-col items-center justify-center py-20 text-center">
          <AlertTriangle className="h-16 w-16 text-red-400 mb-4" />
          <h2 className="text-xl font-semibold text-gray-900 mb-2">Erro ao carregar o imóvel</h2>
          <p className="text-gray-500 mb-6 max-w-md">
            Não foi possível carregar os detalhes deste imóvel. Verifique a sua ligação à internet e tente novamente.
          </p>
          <Link
            to={`/listings/${id}`}
            className="inline-flex items-center gap-2 px-4 py-2 bg-emerald-600 text-white rounded-lg hover:bg-emerald-700 transition-colors text-sm font-medium"
          >
            <Loader2 className="h-4 w-4" />
            Tentar novamente
          </Link>
        </div>
      </div>
    )
  }

  // ── edge case: no data after successful fetch ────────────
  if (!listing) {
    return <NotFoundPage />
  }

  // ── success state ────────────────────────────────────────
  const {
    title,
    price,
    transaction,
    description,
    images,
    bedrooms,
    bathrooms,
    areaM2,
    landAreaM2,
    city,
    parish,
    district,
    listingUrl,
    source,
    createdAt,
  } = listing

  const formatter = new Intl.NumberFormat('pt-PT', {
    style: 'currency',
    currency: 'EUR',
    maximumFractionDigits: 0,
  })

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      {/* Back link */}
      <Link
        to="/search"
        className="inline-flex items-center gap-1.5 text-sm text-emerald-600 hover:text-emerald-700 mb-6 transition-colors"
      >
        <ArrowLeft className="h-4 w-4" />
        Voltar para resultados
      </Link>

      {/* Image gallery — single image placeholder for now */}
      {images && images.length > 0 && (
        <div className="mb-8">
          <img
            src={images[0]}
            alt={title}
            className="w-full h-72 sm:h-96 object-cover rounded-xl"
          />
        </div>
      )}

      {/* Title & price */}
      <div className="mb-8">
        <h1 className="text-2xl sm:text-3xl font-bold text-gray-900 mb-2">
          {title}
        </h1>
        <div className="flex items-center gap-3">
          <span className="text-2xl font-bold text-emerald-700">
            {formatter.format(price)}
          </span>
          <span className={cn(
            'inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium',
            transaction === 'sale' ? 'bg-blue-100 text-blue-700' : 'bg-purple-100 text-purple-700',
          )}>
            {transaction === 'sale' ? 'Venda' : 'Arrendamento'}
          </span>
        </div>
      </div>

      {/* Key details grid */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 mb-8">
        {bedrooms !== undefined && (
          <DetailCard icon={<BedDouble className="h-5 w-5" />} label="Quartos" value={bedrooms} />
        )}
        {bathrooms !== undefined && (
          <DetailCard icon={<Bath className="h-5 w-5" />} label="Casas de banho" value={bathrooms} />
        )}
        {areaM2 !== undefined && (
          <DetailCard icon={<Maximize2 className="h-5 w-5" />} label="Área útil" value={`${areaM2} m²`} />
        )}
        {landAreaM2 !== undefined && (
          <DetailCard icon={<LandPlot className="h-5 w-5" />} label="Terreno" value={`${landAreaM2} m²`} />
        )}
      </div>

      {/* Location */}
      {(city || parish || district) && (
        <div className="flex items-start gap-2 mb-6 text-gray-600">
          <MapPin className="h-5 w-5 mt-0.5 shrink-0 text-gray-400" />
          <p className="text-sm">
            {[parish, city, district].filter(Boolean).join(', ')}
          </p>
        </div>
      )}

      {/* Description */}
      {description && (
        <div className="mb-8">
          <h2 className="text-lg font-semibold text-gray-900 mb-3">Descrição</h2>
          <p className="text-gray-600 leading-relaxed whitespace-pre-line">
            {description}
          </p>
        </div>
      )}

      {/* Original listing link */}
      {listingUrl && (
        <a
          href={listingUrl}
          target="_blank"
          rel="noopener noreferrer"
          className="inline-flex items-center gap-2 text-sm text-emerald-600 hover:text-emerald-700 transition-colors"
        >
          <ExternalLink className="h-4 w-4" />
          Ver anúncio original{source ? ` (${source})` : ''}
        </a>
      )}

      {/* Metadata */}
      {createdAt && (
        <p className="mt-6 text-xs text-gray-400">
          <Calendar className="h-3 w-3 inline mr-1" />
          Publicado em {new Date(createdAt).toLocaleDateString('pt-PT')}
        </p>
      )}
    </div>
  )
}

// ── Sub-components ───────────────────────────────────────────

function DetailCard({ icon, label, value }: { icon: React.ReactNode; label: string; value: string | number }) {
  return (
    <div className="flex items-start gap-3 p-4 bg-gray-50 rounded-lg border border-gray-100">
      <div className="text-gray-400 shrink-0">{icon}</div>
      <div>
        <p className="text-xs text-gray-500">{label}</p>
        <p className="text-sm font-semibold text-gray-900">{value}</p>
      </div>
    </div>
  )
}

function NotFoundPage() {
  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <div className="flex flex-col items-center justify-center py-20 text-center">
        <Home className="h-16 w-16 text-gray-300 mb-4" />
        <h2 className="text-xl font-semibold text-gray-900 mb-2">Imóvel não encontrado</h2>
        <p className="text-gray-500 mb-6 max-w-md">
          O imóvel que procura não existe ou foi removido. Pode procurar outros imóveis disponíveis.
        </p>
        <Link
          to="/search"
          className="inline-flex items-center gap-2 px-4 py-2 bg-emerald-600 text-white rounded-lg hover:bg-emerald-700 transition-colors text-sm font-medium"
        >
          <ArrowLeft className="h-4 w-4" />
          Ver imóveis disponíveis
        </Link>
      </div>
    </div>
  )
}
