import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useLocation, useSearchParams } from 'react-router-dom'
import { AlertCircle, ListFilter, Map, RefreshCw, Search } from 'lucide-react'
import { useListings } from '@/hooks'
import { useFilterStore } from '@/stores/filterStore'
import SearchControls, { SortControl } from '@/components/SearchControls'
import ListingCard from '@/components/listing/ListingCard'
import { Button } from '@/components/ui/button'
import type { ListingSummary } from '@/types/api'

function ListingSkeleton() {
  return <div className="flex overflow-hidden rounded-xl border border-gray-200 bg-white animate-pulse"><div className="h-40 w-44 shrink-0 bg-gray-200 sm:h-48 sm:w-64" /><div className="flex-1 space-y-3 p-5"><div className="h-6 w-32 rounded bg-gray-200" /><div className="h-5 w-3/4 rounded bg-gray-200" /><div className="h-4 w-1/2 rounded bg-gray-100" /><div className="h-6 w-48 rounded bg-gray-100" /></div></div>
}

function Pagination({ page, totalPages, onPageChange }: { page: number; totalPages: number; onPageChange: (page: number) => void }) {
  if (totalPages <= 1) return null
  return <nav className="flex items-center justify-center gap-3 py-7" aria-label="Paginação"><Button variant="outline" size="sm" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>Anterior</Button><span className="text-sm tabular-nums text-gray-600">{page} / {totalPages}</span><Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>Seguinte</Button></nav>
}

function EmptyState() {
  const clearFilters = useFilterStore((state) => state.clearFilters)
  return <div className="py-20 text-center"><div className="mx-auto mb-4 w-fit rounded-full bg-gray-50 p-4"><Search className="size-8 text-gray-400" /></div><h2 className="text-lg font-semibold text-gray-900">Nenhum imóvel encontrado</h2><p className="mx-auto mt-2 max-w-md text-sm text-gray-500">Experimente aumentar o preço máximo ou remover um dos filtros aplicados.</p><Button variant="outline" className="mt-5" onClick={clearFilters}>Limpar filtros</Button></div>
}

export default function SearchPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const location = useLocation()
  const store = useFilterStore()
  const filterParams = store.toParams()
  const paramsKey = JSON.stringify(filterParams)
  const [page, setPage] = useState(1)
  const [hydrated, setHydrated] = useState(false)
  const previousFilters = useRef<string | null>(null)

  useEffect(() => {
    const numeric = (key: string) => searchParams.get(key) ? Number(searchParams.get(key)) : undefined
    store.hydrate({
      type: searchParams.get('type') ?? undefined,
      transaction: searchParams.get('transaction') ?? 'sale',
      priceMin: numeric('minPrice'), priceMax: numeric('maxPrice'), bedrooms: numeric('minBedrooms'), minAreaM2: numeric('minAreaM2'),
      locality: searchParams.get('locality') ?? undefined, agencySlug: searchParams.get('agencySlug') ?? undefined,
      sortBy: searchParams.get('sortBy') ?? undefined,
      sortDirection: searchParams.get('sortDirection') === 'Asc' ? 'Asc' : searchParams.get('sortDirection') === 'Desc' ? 'Desc' : undefined,
    })
    setPage(Math.max(1, Number(searchParams.get('page')) || 1))
    setHydrated(true)
    // The URL is the initial source of truth; later updates flow store → URL.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    if (!hydrated) return
    if (previousFilters.current !== null && previousFilters.current !== paramsKey) setPage(1)
    previousFilters.current = paramsKey
  }, [hydrated, paramsKey])

  useEffect(() => {
    if (!hydrated) return
    const next = new URLSearchParams()
    Object.entries(filterParams).forEach(([key, value]) => { if (value !== undefined && value !== null) next.set(key, String(value)) })
    if (page > 1) next.set('page', String(page))
    if (next.toString() !== searchParams.toString()) setSearchParams(next, { replace: true })
  }, [filterParams, hydrated, page, searchParams, setSearchParams])

  const { data, isLoading, isError, error, refetch, isFetching } = useListings({ ...filterParams, page, pageSize: 12 })
  const properties: ListingSummary[] = data?.items ?? []
  const changePage = useCallback((next: number) => { setPage(next); window.scrollTo({ top: 0, behavior: 'smooth' }) }, [])
  const returnState = { returnTo: `${location.pathname}${location.search}`, scrollY: window.scrollY }

  return <div className="min-h-full bg-white">
    <SearchControls />
    <main className="mx-auto max-w-4xl px-4 py-7 sm:px-6 lg:px-8">
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3 border-b border-slate-200 pb-4">
        <div><h1 className="text-xl font-bold text-slate-900">Imóveis em Pombal</h1>{data && <p className="mt-0.5 text-sm text-slate-600">{data.totalCount.toLocaleString('pt-PT')} {data.totalCount === 1 ? 'imóvel encontrado' : 'imóveis encontrados'}</p>}</div>
        <div className="flex items-center gap-3"><span className="hidden items-center gap-1 text-sm text-sky-800 sm:flex"><ListFilter className="size-4" />Ordenar:</span><SortControl /><Link to={`/map${location.search}`} state={returnState}><Button variant="outline" className="gap-2 rounded border-sky-700 text-sky-800 hover:bg-sky-50"><Map className="size-4" />Mapa</Button></Link></div>
      </div>
      {isFetching && !isLoading && <p className="mb-3 flex items-center gap-2 text-xs text-gray-500"><RefreshCw className="size-3 animate-spin" />A atualizar resultados...</p>}
      {isLoading && <div className="space-y-4">{Array.from({ length: 5 }).map((_, index) => <ListingSkeleton key={index} />)}</div>}
      {isError && !isLoading && <div className="py-20 text-center"><AlertCircle className="mx-auto mb-4 size-9 text-red-500" /><h2 className="font-semibold">Erro ao carregar imóveis</h2><p className="mt-2 text-sm text-gray-500">{error instanceof Error ? error.message : 'Ocorreu um erro inesperado.'}</p><Button className="mt-5" onClick={() => refetch()}>Tentar novamente</Button></div>}
      {!isLoading && !isError && (properties.length ? <><div className="space-y-5">{properties.map((property) => <Link key={property.id} to={`/listings/${property.id}`} state={returnState} className="block"><ListingCard property={property} /></Link>)}</div><Pagination page={page} totalPages={data?.totalPages ?? 1} onPageChange={changePage} /></> : <EmptyState />)}
    </main>
  </div>
}
