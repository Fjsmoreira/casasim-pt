import { useState, useCallback, useEffect, useRef } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { MapContainer, TileLayer } from 'react-leaflet'
import L from 'leaflet'

// Fix default marker icons in webpack/vite builds
import markerIcon2x from 'leaflet/dist/images/marker-icon-2x.png'
import markerIcon from 'leaflet/dist/images/marker-icon.png'
import markerShadow from 'leaflet/dist/images/marker-shadow.png'

delete (L.Icon.Default.prototype as { _getIconUrl?: unknown })._getIconUrl
L.Icon.Default.mergeOptions({
  iconRetinaUrl: markerIcon2x,
  iconUrl: markerIcon,
  shadowUrl: markerShadow,
})

import { useListings } from '@/hooks'
import { useFilterStore } from '@/stores/filterStore'
import FilterSidebar, { FilterMobileTrigger } from '@/components/FilterSidebar'
import ListingCard from '@/components/listing/ListingCard'
import { Button } from '@/components/ui/button'
import { ArrowLeft, ArrowRight, Search, AlertCircle, RefreshCw } from 'lucide-react'
import type { ListingSummary } from '@/types/api'

/* ─────── Skeleton card for loading state ─────── */

function ListingCardSkeleton() {
  return (
    <div className="bg-white rounded-xl border border-gray-200 overflow-hidden animate-pulse">
      <div className="aspect-[4/3] bg-gray-200" />
      <div className="p-4 space-y-3">
        <div className="h-6 w-24 bg-gray-200 rounded" />
        <div className="h-4 w-full bg-gray-200 rounded" />
        <div className="h-3 w-32 bg-gray-200 rounded" />
        <div className="flex gap-2 pt-1">
          <div className="h-5 w-14 bg-gray-100 rounded-md" />
          <div className="h-5 w-16 bg-gray-100 rounded-md" />
        </div>
      </div>
    </div>
  )
}

function ListingGridSkeleton() {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-5">
      {Array.from({ length: 6 }).map((_, i) => (
        <ListingCardSkeleton key={i} />
      ))}
    </div>
  )
}

/* ─────── Pagination ─────── */

interface PaginationProps {
  page: number
  totalPages: number
  onPageChange: (page: number) => void
}

function Pagination({ page, totalPages, onPageChange }: PaginationProps) {
  if (totalPages <= 1) return null

  return (
    <nav
      className="flex items-center justify-center gap-2 pt-6 pb-2"
      aria-label="Paginação"
    >
      <Button
        variant="outline"
        size="sm"
        disabled={page <= 1}
        onClick={() => onPageChange(page - 1)}
        className="gap-1"
      >
        <ArrowLeft className="size-4" />
        Anterior
      </Button>

      <span className="text-sm text-muted-foreground px-3 tabular-nums">
        {page} / {totalPages}
      </span>

      <Button
        variant="outline"
        size="sm"
        disabled={page >= totalPages}
        onClick={() => onPageChange(page + 1)}
        className="gap-1"
      >
        Seguinte
        <ArrowRight className="size-4" />
      </Button>
    </nav>
  )
}

/* ─────── Error state ─────── */

function ErrorState({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div className="flex flex-col items-center justify-center py-20 text-center">
      <div className="rounded-full bg-red-50 p-4 mb-4">
        <AlertCircle className="size-8 text-red-500" />
      </div>
      <h2 className="text-lg font-semibold text-gray-900 mb-2">
        Erro ao carregar imóveis
      </h2>
      <p className="text-sm text-muted-foreground max-w-md mb-6">
        {message}
      </p>
      <Button onClick={onRetry} className="gap-2">
        <RefreshCw className="size-4" />
        Tentar novamente
      </Button>
    </div>
  )
}

/* ─────── Empty state ─────── */

function EmptyState() {
  return (
    <div className="flex flex-col items-center justify-center py-20 text-center">
      <div className="rounded-full bg-gray-50 p-4 mb-4">
        <Search className="size-8 text-gray-400" />
      </div>
      <h2 className="text-lg font-semibold text-gray-900 mb-2">
        Nenhum imóvel encontrado
      </h2>
      <p className="text-sm text-muted-foreground max-w-md">
        Nenhum imóvel corresponde aos filtros selecionados. Tente ajustar ou
        limpar os filtros para ver mais resultados.
      </p>
    </div>
  )
}

/* ─────── Main SearchPage ─────── */

export default function SearchPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const filterStore = useFilterStore()
  const filterParams = filterStore.toParams()
  const pageFromUrl = searchParams.get('page') ? Number(searchParams.get('page')) : 1
  const [page, setPage] = useState(pageFromUrl)

  /* ── Sync URL → store on mount / URL change ── */
  useEffect(() => {
    const urlType = searchParams.get('type') ?? undefined
    const urlTransaction = searchParams.get('transaction') ?? undefined
    const urlMinPrice = searchParams.get('minPrice') ? Number(searchParams.get('minPrice')) : undefined
    const urlMaxPrice = searchParams.get('maxPrice') ? Number(searchParams.get('maxPrice')) : undefined
    const urlBedrooms = searchParams.get('bedrooms') ? Number(searchParams.get('bedrooms')) : undefined
    const urlPage = searchParams.get('page') ? Number(searchParams.get('page')) : 1

    // Only sync if the URL has any filter params (avoids overwriting defaults on first load)
    if (searchParams.size > 0) {
      if (urlType !== filterStore.type) filterStore.setType(urlType)
      if (urlTransaction !== filterStore.transaction) filterStore.setTransaction(urlTransaction)
      if (urlMinPrice !== filterStore.priceMin) filterStore.setPriceMin(urlMinPrice)
      if (urlMaxPrice !== filterStore.priceMax) filterStore.setPriceMax(urlMaxPrice)
      if (urlBedrooms !== filterStore.bedrooms) filterStore.setBedrooms(urlBedrooms)
      setPage(urlPage)
    }
    // Run only once on mount — intentionally no deps so URL drives initial state
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  /* ── Sync store → URL when filters change ── */
  const paramsKey = JSON.stringify(filterParams)
  const isFirstRender = useRef(true)
  useEffect(() => {
    if (isFirstRender.current) {
      isFirstRender.current = false
      return
    }
    setSearchParams(
      (prev) => {
        const next = new URLSearchParams(prev)
        for (const [key, val] of Object.entries(filterParams)) {
          if (val !== undefined && val !== null) {
            next.set(key, String(val))
          } else {
            next.delete(key)
          }
        }
        // Reset page when filters change
        next.delete('page')
        return next
      },
      { replace: true },
    )
  }, [paramsKey, setSearchParams])

  /* ── Sync page → URL ── */
  useEffect(() => {
    setSearchParams(
      (prev) => {
        if (page > 1) prev.set('page', String(page))
        else prev.delete('page')
        return prev
      },
      { replace: true },
    )
  }, [page, setSearchParams])

  const { data, isLoading, isError, error, refetch, isFetching } = useListings({
    ...filterParams,
    page,
    pageSize: 12,
  })

  const properties: ListingSummary[] = data?.items ?? []
  const totalPages = data?.totalPages ?? 1
  const totalCount = data?.totalCount ?? 0
  const isInitialLoad = isLoading && !data
  const isRefreshing = isFetching && !isLoading

  const handleRetry = useCallback(() => {
    refetch()
  }, [refetch])

  const handlePageChange = useCallback((newPage: number) => {
    setPage(newPage)
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }, [])

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Imóveis em Pombal</h1>
          {data && !isInitialLoad && (
            <p className="text-sm text-muted-foreground mt-0.5">
              {totalCount} {totalCount === 1 ? 'imóvel encontrado' : 'imóveis encontrados'}
            </p>
          )}
        </div>
        <FilterMobileTrigger />
      </div>

      {/* ── Loading (first load) ── */}
      {isInitialLoad && <ListingGridSkeleton />}

      {/* ── Error ── */}
      {isError && !isInitialLoad && (
        <ErrorState
          message={error instanceof Error ? error.message : 'Ocorreu um erro inesperado ao carregar a listagem.'}
          onRetry={handleRetry}
        />
      )}

      {/* ── Empty (successful load, no results) ── */}
      {!isInitialLoad && !isError && properties.length === 0 && <EmptyState />}

      {/* ── Results ── */}
      {!isInitialLoad && !isError && properties.length > 0 && (
        <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
          {/* Filter sidebar — desktop */}
          <FilterSidebar className="lg:col-span-1" />

          {/* Listing grid + map */}
          <div className="lg:col-span-3 space-y-4">
            {/* Subtle refresh indicator */}
            {isRefreshing && (
              <div className="flex items-center gap-2 text-xs text-muted-foreground pb-1">
                <RefreshCw className="size-3 animate-spin" />
                <span>A atualizar...</span>
              </div>
            )}

            {/* Listing grid */}
            <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-5">
              {properties.map((p) => (
                <Link
                  key={p.id}
                  to={`/listings/${p.id}`}
                  className="block transition-opacity"
                >
                  <ListingCard property={p} />
                </Link>
              ))}
            </div>

            {/* Pagination */}
            <Pagination
              page={page}
              totalPages={totalPages}
              onPageChange={handlePageChange}
            />

            {/* Map */}
            <div className="h-[400px] rounded-lg overflow-hidden border border-gray-200 lg:h-[500px] lg:sticky lg:top-8">
              <MapContainer
                center={[39.915, -8.628]}
                zoom={13}
                className="h-full w-full"
                scrollWheelZoom={false}
              >
                <TileLayer
                  attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
                  url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
                />
              </MapContainer>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
