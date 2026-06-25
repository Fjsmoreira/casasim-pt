import { useParams, Link, useLocation } from 'react-router-dom'
import { useListing } from '@/hooks/useListing'
import { ArrowLeft, Loader2, Home, AlertTriangle, Calendar } from 'lucide-react'

import PropertyGallery from '@/components/PropertyGallery'
import PropertyHeader from '@/components/PropertyHeader'
import PropertyFacts from '@/components/PropertyFacts'
import PropertyDescription from '@/components/PropertyDescription'
import PropertyFeatures from '@/components/PropertyFeatures'
import PropertyLocationMap, { LocationUnavailable } from '@/components/PropertyLocationMap'
import AgencyCard from '@/components/AgencyCard'
import { formatListingPrice } from '@/lib/utils'

export default function PropertyDetailPage() {
  const { id } = useParams()
  const location = useLocation()
  const { data: listing, isLoading, isError, error } = useListing(id!)
  const returnState = location.state as { returnTo?: string; scrollY?: number } | null
  const returnTo = returnState?.returnTo ?? '/search'

  // ── 404 state ──────────────────────────────────────────────
  if (isError && error && 'response' in error && (error as { response: { status: number } }).response?.status === 404) {
    return <NotFoundPage />
  }

  // ── loading state ─────────────────────────────────────────
  if (isLoading) {
    return <LoadingSkeleton />
  }

  // ── generic error state ───────────────────────────────────
  if (isError) {
    return <ErrorPage id={id!} />
  }

  // ── edge case: no data after successful fetch ────────────
  if (!listing) {
    return <NotFoundPage />
  }

  const {
    title,
    price,
    transaction,
    type,
    description,
    images,
    bedrooms,
    bathrooms,
    areaM2,
    landAreaM2,
    city,
    parish,
    district,
    features,
    sourceUrl,
    latitude,
    longitude,
    createdAt,
    agencyName,
    agencyPhone,
    agencyEmail,
  } = listing

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      {/* Back link */}
      <Link
        to={returnTo}
        onClick={() => {
          const scrollY = returnState?.scrollY
          if (scrollY !== undefined) window.setTimeout(() => window.scrollTo(0, scrollY), 0)
        }}
        className="inline-flex items-center gap-1.5 text-sm text-sky-600 hover:text-sky-700 mb-6 transition-colors"
      >
        <ArrowLeft className="h-4 w-4" />
        Voltar para resultados
      </Link>

      {/* Gallery */}
      {images && images.length > 0 && (
        <div className="mb-8">
          <PropertyGallery images={images} title={title} />
        </div>
      )}

      {/* Header: title, price, badge, location, source link */}
      <PropertyHeader
        title={title}
        price={price}
        transaction={transaction}
        areaM2={areaM2}
        parish={parish}
        city={city}
        district={district}
        listingUrl={sourceUrl}
        source={agencyName}
      />

      {/* Facts grid */}
      <PropertyFacts
        bedrooms={bedrooms}
        bathrooms={bathrooms}
        areaM2={areaM2}
        landAreaM2={landAreaM2}
        propertyType={type}
        createdAt={createdAt}
      />

      {/* Description */}
      {description && <PropertyDescription description={description} />}

      {/* Features */}
      {features && features.length > 0 && <PropertyFeatures features={features} />}

      {/* Small location map */}
      {latitude != null && longitude != null ? (
        <PropertyLocationMap
          latitude={latitude}
          longitude={longitude}
          title={title}
          price={formatListingPrice(price, transaction)}
        />
      ) : (
        <LocationUnavailable />
      )}

      {/* Agency card */}
      <AgencyCard
        name={agencyName}
        phone={agencyPhone}
        email={agencyEmail}
        source={agencyName}
      />

      {/* Metadata footer */}
      {createdAt && (
        <p className="text-xs text-gray-400 mt-6 pt-6 border-t border-gray-100">
          <Calendar className="h-3 w-3 inline mr-1" />
          Publicado em {new Date(createdAt).toLocaleDateString('pt-PT')}
        </p>
      )}
    </div>
  )
}

// ── Sub-pages ────────────────────────────────────────────────

function LoadingSkeleton() {
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

function ErrorPage({ id }: { id: string }) {
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
          className="inline-flex items-center gap-2 px-4 py-2 bg-sky-600 text-white rounded-lg hover:bg-sky-700 transition-colors text-sm font-medium"
        >
          <Loader2 className="h-4 w-4" />
          Tentar novamente
        </Link>
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
          className="inline-flex items-center gap-2 px-4 py-2 bg-sky-600 text-white rounded-lg hover:bg-sky-700 transition-colors text-sm font-medium"
        >
          <ArrowLeft className="h-4 w-4" />
          Ver imóveis disponíveis
        </Link>
      </div>
    </div>
  )
}
