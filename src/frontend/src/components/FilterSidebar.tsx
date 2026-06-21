import { useFilterStore } from '@/stores/filterStore'
import { useAgencies } from '@/hooks'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import { SlidersHorizontal, X } from 'lucide-react'
import { useCallback, useId, type ChangeEvent } from 'react'

const PROPERTY_TYPES = [
  { value: 'house', label: 'Moradia' },
  { value: 'apartment', label: 'Apartamento' },
  { value: 'land', label: 'Terreno' },
  { value: 'commercial', label: 'Comercial' },
  { value: 'other', label: 'Outro' },
] as const

const BEDROOM_OPTIONS = [
  { value: 1, label: '1' },
  { value: 2, label: '2' },
  { value: 3, label: '3' },
  { value: 4, label: '4+' },
] as const

const SORT_OPTIONS = [
  { value: 'PublishedAt', label: 'Mais recentes' },
  { value: 'Price', label: 'Preço' },
  { value: 'AreaM2', label: 'Área' },
] as const

const LOCALITIES = [
  'Pombal',
  'Abiul',
  'Albergaria dos Doze',
  'Almagreira',
  'Carnide',
  'Carriço',
  'Louriçal',
  'Meirinhas',
  'Pelariga',
  'Redinha',
  'Santiago e São Simão de Litém e Albergaria dos Doze',
  'São Simão de Litém',
  'Vermoil',
  'Vila Cã',
] as const

/* ─────────────── shared input class ─────────────── */

const inputCls =
  'w-full rounded-md border border-input bg-background px-3 py-2 text-sm shadow-sm ' +
  'placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring'

/* ─────────────── inner filter form content ─────────────── */

function FilterContent({ className }: { className?: string }) {
  const { data: agencies = [] } = useAgencies()
  const {
    priceMin, priceMax,
    type, bedrooms, minAreaM2, locality, agencySlug, sortBy,
    setPriceMin, setPriceMax, setType, setBedrooms, setMinAreaM2, setLocality, setAgencySlug, setSortBy,
    clearFilters,
  } = useFilterStore()

  const anyActive = priceMin !== undefined || priceMax !== undefined ||
    type !== undefined || bedrooms !== undefined || minAreaM2 !== undefined ||
    locality !== undefined || agencySlug !== undefined || sortBy !== undefined

  const handleBedroomChange = useCallback(
    (e: ChangeEvent<HTMLSelectElement>) => {
      const v = e.target.value
      setBedrooms(v ? Number(v) : undefined)
    },
    [setBedrooms],
  )

  const selectedTypes = type?.split(',').filter(Boolean) ?? []
  const toggleType = (value: string) => {
    const next = selectedTypes.includes(value)
      ? selectedTypes.filter((selected) => selected !== value)
      : [...selectedTypes, value]
    setType(next.length ? next.join(',') : undefined)
  }

  return (
    <div className={cn('space-y-5', className)}>
      {/* ── Sort ── */}
      <fieldset>
        <legend className="text-sm font-medium text-foreground mb-2">Ordenar</legend>
        <select
          aria-label="Ordenar"
          value={sortBy ?? ''}
          onChange={(e) => setSortBy(e.target.value || undefined)}
          className={inputCls + ' appearance-none'}
        >
          <option value="">Atualizados recentemente</option>
          {SORT_OPTIONS.map((option) => (
            <option key={option.value} value={option.value}>{option.label}</option>
          ))}
        </select>
      </fieldset>

      {/* ── Property type ── */}
      <fieldset>
        <legend className="text-sm font-medium text-foreground mb-2">Tipo de imóvel</legend>
        <div className="flex flex-wrap gap-1.5">
          {PROPERTY_TYPES.map((pt) => (
            <button
              key={pt.value}
              type="button"
              onClick={() => toggleType(pt.value)}
              className={cn(
                'rounded-md border px-2.5 py-1 text-xs font-medium transition-colors',
                selectedTypes.includes(pt.value)
                  ? 'border-sky-700 bg-sky-50 text-sky-800'
                  : 'border-input bg-background text-muted-foreground hover:bg-accent',
              )}
            >
              {pt.label}
            </button>
          ))}
        </div>
      </fieldset>

      {/* ── Price range ── */}
      <fieldset>
        <legend className="text-sm font-medium text-foreground mb-2">Faixa de preço (€)</legend>
        <div className="flex items-center gap-2">
          <input
            type="number"
            min={0}
            step={10000}
            placeholder="Mín."
            value={priceMin ?? ''}
            onChange={(e) => setPriceMin(e.target.value ? Number(e.target.value) : undefined)}
            className={inputCls}
          />
          <span className="text-muted-foreground">–</span>
          <input
            type="number"
            min={0}
            step={10000}
            placeholder="Máx."
            value={priceMax ?? ''}
            onChange={(e) => setPriceMax(e.target.value ? Number(e.target.value) : undefined)}
            className={inputCls}
          />
        </div>
      </fieldset>

      {/* ── Locality / parish ── */}
      <fieldset>
        <legend className="text-sm font-medium text-foreground mb-2">Localidade</legend>
        <select
          aria-label="Localidade"
          value={locality ?? ''}
          onChange={(e) => setLocality(e.target.value || undefined)}
          className={inputCls + ' appearance-none'}
        >
          <option value="">Todas</option>
          {LOCALITIES.map((loc) => (
            <option key={loc} value={loc}>{loc}</option>
          ))}
        </select>
      </fieldset>

      {/* ── Agency ── */}
      <fieldset>
        <legend className="text-sm font-medium text-foreground mb-2">Agência</legend>
        <select
          aria-label="Agência"
          value={agencySlug ?? ''}
          onChange={(e) => setAgencySlug(e.target.value || undefined)}
          className={inputCls + ' appearance-none'}
        >
          <option value="">Todas</option>
          {agencies.map((agency) => (
            <option key={agency.id} value={agency.slug}>{agency.name}</option>
          ))}
        </select>
      </fieldset>

      {/* ── Bedrooms ── */}
      <fieldset>
        <legend className="text-sm font-medium text-foreground mb-2">Quartos</legend>
        <select
          aria-label="Quartos"
          value={bedrooms ?? ''}
          onChange={handleBedroomChange}
          className={inputCls + ' appearance-none'}
        >
          <option value="">Qualquer</option>
          {BEDROOM_OPTIONS.map((b) => (
            <option key={b.value} value={b.value}>{b.label}</option>
          ))}
        </select>
      </fieldset>

      {/* ── Area ── */}
      <fieldset>
        <legend className="text-sm font-medium text-foreground mb-2">Área mínima (m²)</legend>
        <input
          type="number"
          min={0}
          step={10}
          placeholder="Ex. 100"
          value={minAreaM2 ?? ''}
          onChange={(e) => setMinAreaM2(e.target.value ? Number(e.target.value) : undefined)}
          className={inputCls}
        />
      </fieldset>

      {/* ── Clear filters ── */}
      {anyActive && (
        <Button
          variant="outline"
          size="sm"
          className="w-full"
          onClick={clearFilters}
        >
          Limpar filtros
        </Button>
      )}
    </div>
  )
}

/* ─────────────── mobile trigger button ─────────────── */

export function FilterMobileTrigger() {
  const setMobileOpen = useFilterStore((s) => s.setMobileOpen)
  return (
    <Button
      variant="outline"
      size="sm"
      onClick={() => setMobileOpen(true)}
      className="lg:hidden"
    >
      <SlidersHorizontal className="size-4" />
      Filtros
    </Button>
  )
}

/* ─────────────── mobile overlay / drawer ─────────────── */

function FilterMobileSheet() {
  const mobileOpen = useFilterStore((s) => s.mobileOpen)
  const setMobileOpen = useFilterStore((s) => s.setMobileOpen)
  const sheetId = useId()

  return (
    <>
      {/* Backdrop */}
      {mobileOpen && (
        <div
          className="fixed inset-0 z-40 bg-black/40 transition-opacity lg:hidden"
          onClick={() => setMobileOpen(false)}
        />
      )}

      {/* Sheet panel */}
      <div
        id={sheetId}
        role="dialog"
        aria-modal={mobileOpen}
        aria-label="Filtros"
        className={cn(
          'fixed inset-y-0 left-0 z-50 w-80 max-w-[85vw] bg-background shadow-xl transition-transform duration-300 ease-in-out lg:hidden',
          mobileOpen ? 'translate-x-0' : '-translate-x-full',
        )}
      >
        <div className="flex items-center justify-between border-b px-4 py-3">
          <span className="text-sm font-semibold text-foreground">Filtros</span>
          <button
            type="button"
            onClick={() => setMobileOpen(false)}
            className="rounded-md p-1 text-muted-foreground hover:bg-accent"
          >
            <X className="size-4" />
          </button>
        </div>
        <div className="overflow-y-auto px-4 py-4">
          <FilterContent />
        </div>
      </div>
    </>
  )
}

/* ─────────────── main exported component ─────────────── */

interface FilterSidebarProps {
  className?: string
}

export default function FilterSidebar({ className }: FilterSidebarProps) {
  return (
    <>
      {/* Desktop sidebar */}
      <aside className={cn('hidden lg:block', className)}>
        <div className="rounded-lg border bg-card p-4 text-card-foreground shadow-sm">
          <h2 className="flex items-center gap-2 text-sm font-semibold text-foreground mb-4">
            <SlidersHorizontal className="size-4" />
            Filtros
          </h2>
          <FilterContent />
        </div>
      </aside>

      {/* Mobile sheet */}
      <FilterMobileSheet />
    </>
  )
}
