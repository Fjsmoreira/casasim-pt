import { useState, type ChangeEvent } from 'react'
import { ChevronDown, Search, SlidersHorizontal, X } from 'lucide-react'
import { useFilterStore } from '@/stores/filterStore'
import { useAgencies } from '@/hooks'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

const LOCALITIES = [
  'Pombal', 'Abiul', 'Albergaria dos Doze', 'Almagreira', 'Carnide', 'Carriço',
  'Louriçal', 'Meirinhas', 'Pelariga', 'Redinha', 'São Simão de Litém', 'Vermoil', 'Vila Cã',
] as const

const PROPERTY_TYPES = [
  { value: 'house', label: 'Moradia' },
  { value: 'apartment', label: 'Apartamento' },
  { value: 'land', label: 'Terreno' },
  { value: 'commercial', label: 'Comercial' },
] as const

const DEAL_LABELS = [
  { value: 'GoodDeal', label: 'Bom negócio' },
  { value: 'BadDeal', label: 'Mau negócio' },
] as const

const inputClass = 'h-10 w-full rounded border border-slate-300 bg-white px-3 text-sm text-slate-700 outline-none transition focus:border-[#007ca8] focus:ring-2 focus:ring-sky-100'

function FilterChips() {
  const store = useFilterStore()
  const selectedTypes = store.type?.split(',').filter(Boolean) ?? []
  const chips = [
    store.locality && { label: store.locality, clear: () => store.setLocality(undefined) },
    (store.priceMin !== undefined || store.priceMax !== undefined) && { label: `${store.priceMin ? `€${store.priceMin.toLocaleString('pt-PT')}` : 'Qualquer'} – ${store.priceMax ? `€${store.priceMax.toLocaleString('pt-PT')}` : 'Qualquer'}`, clear: () => { store.setPriceMin(undefined); store.setPriceMax(undefined) } },
    selectedTypes.length > 0 && { label: selectedTypes.map((value) => PROPERTY_TYPES.find((item) => item.value === value)?.label ?? value).join(' ou '), clear: () => store.setType(undefined) },
    store.bedrooms !== undefined && { label: `${store.bedrooms}+ quartos`, clear: () => store.setBedrooms(undefined) },
    store.minAreaM2 !== undefined && { label: `${store.minAreaM2}+ m²`, clear: () => store.setMinAreaM2(undefined) },
    store.agencySlug && { label: 'Agência selecionada', clear: () => store.setAgencySlug(undefined) },
    store.dealLabel && { label: DEAL_LABELS.find((deal) => deal.value === store.dealLabel)?.label ?? store.dealLabel, clear: () => store.setDealLabel(undefined) },
  ].filter(Boolean) as { label: string; clear: () => void }[]

  if (!chips.length) return null
  return (
    <div className="flex flex-wrap items-center gap-2 pt-3" aria-label="Filtros aplicados">
      {chips.map((chip) => (
        <button key={chip.label} type="button" onClick={chip.clear} className="inline-flex items-center gap-1 rounded-full bg-sky-50 px-3 py-1 text-xs font-medium text-sky-800 hover:bg-sky-100">
          {chip.label}<X className="size-3" />
        </button>
      ))}
      <button type="button" onClick={store.clearFilters} className="text-xs font-medium text-sky-700 hover:text-sky-900">Limpar tudo</button>
    </div>
  )
}

function PropertyTypeSelect() {
  const store = useFilterStore()
  const [open, setOpen] = useState(false)
  const selectedTypes = store.type?.split(',').filter(Boolean) ?? []
  const selectedLabel = selectedTypes.length === 0
    ? 'Tipo de imóvel'
    : selectedTypes.map((value) => PROPERTY_TYPES.find((type) => type.value === value)?.label ?? value).join(' ou ')

  const toggleType = (value: string) => {
    const next = selectedTypes.includes(value)
      ? selectedTypes.filter((type) => type !== value)
      : [...selectedTypes, value]
    store.setType(next.length ? next.join(',') : undefined)
  }

  return <div className="relative">
    <button type="button" aria-label="Tipo de imóvel" aria-expanded={open} onClick={() => setOpen(!open)} className={inputClass + ' flex items-center justify-between text-left'}>
      <span className="truncate">{selectedLabel}</span><ChevronDown className="ml-2 size-4 shrink-0" />
    </button>
    {open && <div className="absolute z-30 mt-2 w-full min-w-52 rounded-lg border border-sky-200 bg-white p-2 shadow-lg">
      {PROPERTY_TYPES.map((type) => <label key={type.value} className="flex cursor-pointer items-center gap-2 rounded px-2 py-1.5 text-sm text-slate-700 hover:bg-sky-50">
        <input type="checkbox" checked={selectedTypes.includes(type.value)} onChange={() => toggleType(type.value)} className="size-4 accent-sky-700" />
        {type.label}
      </label>)}
    </div>}
  </div>
}

function MoreFilters({ compact = false }: { compact?: boolean }) {
  const store = useFilterStore()
  const { data: agencies = [] } = useAgencies()
  const [open, setOpen] = useState(compact)
  const handleBedrooms = (event: ChangeEvent<HTMLSelectElement>) => store.setBedrooms(event.target.value ? Number(event.target.value) : undefined)

  return (
    <div className={cn(compact ? 'space-y-4' : 'relative')}>
      {!compact && <Button type="button" variant="outline" className="h-10 gap-2 rounded border-sky-700 px-4 text-sky-800 hover:bg-sky-50" onClick={() => setOpen(!open)} aria-expanded={open}><SlidersHorizontal className="size-4" />Mais filtros<ChevronDown className={cn('size-4 transition-transform', open && 'rotate-180')} /></Button>}
      {open && (
        <div className={cn(compact ? 'space-y-4' : 'absolute right-0 z-30 mt-2 w-[22rem] rounded-xl border border-gray-200 bg-white p-4 shadow-xl')}>
          <div className="grid grid-cols-2 gap-3">
            <label className="text-sm font-medium text-gray-700">Quartos
              <select value={store.bedrooms ?? ''} onChange={handleBedrooms} className={inputClass + ' mt-1'}>
                <option value="">Qualquer</option><option value="1">1+</option><option value="2">2+</option><option value="3">3+</option><option value="4">4+</option>
              </select>
            </label>
            <label className="text-sm font-medium text-gray-700">Área mínima
              <input type="number" min="0" step="10" placeholder="Ex. 100" value={store.minAreaM2 ?? ''} onChange={(e) => store.setMinAreaM2(e.target.value ? Number(e.target.value) : undefined)} className={inputClass + ' mt-1'} />
            </label>
          </div>
          <label className="block text-sm font-medium text-gray-700">Agência
            <select value={store.agencySlug ?? ''} onChange={(e) => store.setAgencySlug(e.target.value || undefined)} className={inputClass + ' mt-1'}>
              <option value="">Todas as agências</option>
              {agencies.map((agency) => <option key={agency.id} value={agency.slug}>{agency.name}</option>)}
            </select>
          </label>
          <label className="block text-sm font-medium text-gray-700">Análise AI
            <select value={store.dealLabel ?? ''} onChange={(e) => store.setDealLabel((e.target.value || undefined) as typeof store.dealLabel)} className={inputClass + ' mt-1'}>
              <option value="">Todos</option>
              {DEAL_LABELS.map((deal) => <option key={deal.value} value={deal.value}>{deal.label}</option>)}
            </select>
          </label>
        </div>
      )}
    </div>
  )
}

export function SortControl() {
  const store = useFilterStore()
  const value = `${store.sortBy ?? 'UpdatedAt'}:${store.sortDirection ?? 'Desc'}`
  return <label className="flex items-center gap-2 text-sm text-gray-600">Ordenar
    <select aria-label="Ordenar resultados" value={value} onChange={(e) => { const [sortBy, sortDirection] = e.target.value.split(':'); store.setSortBy(sortBy); store.setSortDirection(sortDirection as 'Asc' | 'Desc') }} className="rounded-md border border-gray-200 bg-white px-2 py-1.5 text-sm font-medium text-gray-800 outline-none focus:border-sky-700">
      <option value="UpdatedAt:Desc">Mais recentes</option><option value="Price:Asc">Preço mais baixo</option><option value="Price:Desc">Preço mais alto</option><option value="AreaM2:Desc">Maior área</option>
    </select>
  </label>
}

export default function SearchControls() {
  const store = useFilterStore()
  const [mobileOpen, setMobileOpen] = useState(false)

  return <>
    <section className="sticky top-0 z-20 border-b border-slate-200 bg-white/95 py-3 shadow-sm backdrop-blur" aria-label="Pesquisa de imóveis">
      <div className="mx-auto max-w-4xl px-4 sm:px-6">
        <label className="sr-only" htmlFor="locality">Localidade</label>
        <div className="flex"><input id="locality" list="pombal-localities" value={store.locality ?? ''} onChange={(e) => store.setLocality(e.target.value || undefined)} placeholder="Pesquisar por localidade, bairro ou código postal" className={inputClass + ' rounded-r-none'} /><button type="button" aria-label="Pesquisar" className="grid h-10 w-11 shrink-0 place-items-center rounded-r bg-[#4f8fb3] text-white hover:bg-sky-700"><Search className="size-4" /></button></div>
        <datalist id="pombal-localities">{LOCALITIES.map((locality) => <option key={locality} value={locality} />)}</datalist>
      </div>
      <div className="mx-auto mt-3 grid max-w-4xl grid-cols-2 gap-2 px-4 sm:grid-cols-4 sm:px-6">
        <input aria-label="Preço mínimo" type="number" min="0" step="10000" value={store.priceMin ?? ''} onChange={(e) => store.setPriceMin(e.target.value ? Number(e.target.value) : undefined)} placeholder="Preço mín." className={inputClass} />
        <input aria-label="Preço máximo" type="number" min="0" step="10000" value={store.priceMax ?? ''} onChange={(e) => store.setPriceMax(e.target.value ? Number(e.target.value) : undefined)} placeholder="Preço máx." className={inputClass} />
        <PropertyTypeSelect />
        <div className="hidden sm:block"><MoreFilters /></div>
        <Button type="button" variant="outline" onClick={() => setMobileOpen(true)} className="h-10 w-full gap-2 sm:hidden"><SlidersHorizontal className="size-4" />Filtros</Button>
      </div>
      <div className="mx-auto max-w-4xl px-4 sm:px-6"><FilterChips /></div>
    </section>
    {mobileOpen && <div className="fixed inset-0 z-50 bg-black/40 p-4 md:hidden" onClick={() => setMobileOpen(false)}><div role="dialog" aria-modal="true" aria-label="Mais filtros" className="absolute inset-x-0 bottom-0 max-h-[80vh] overflow-y-auto rounded-t-2xl bg-white p-5" onClick={(e) => e.stopPropagation()}><div className="mb-5 flex items-center justify-between"><h2 className="font-semibold">Mais filtros</h2><button type="button" onClick={() => setMobileOpen(false)} aria-label="Fechar filtros"><X /></button></div><MoreFilters compact /><Button type="button" className="mt-6 w-full" onClick={() => setMobileOpen(false)}>Ver resultados</Button></div></div>}
  </>
}
