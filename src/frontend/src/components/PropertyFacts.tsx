import { BedDouble, Bath, Maximize2, LandPlot, Building2, Calendar } from 'lucide-react'
import { cn } from '@/lib/utils'

interface PropertyFactsProps {
  bedrooms?: number
  bathrooms?: number
  areaM2?: number
  landAreaM2?: number
  propertyType: string
  createdAt?: string
}

interface FactItem {
  icon: React.ReactNode
  label: string
  value: string | number
}

export default function PropertyFacts({
  bedrooms,
  bathrooms,
  areaM2,
  landAreaM2,
  propertyType,
  createdAt,
}: PropertyFactsProps) {
  const typeLabels: Record<string, string> = {
    house: 'Casa',
    apartment: 'Apartamento',
    land: 'Terreno',
    commercial: 'Comercial',
    other: 'Outro',
  }

  const facts: FactItem[] = []

  if (bedrooms !== undefined) {
    facts.push({ icon: <BedDouble className="h-5 w-5" />, label: 'Quartos', value: bedrooms })
  }
  if (bathrooms !== undefined) {
    facts.push({ icon: <Bath className="h-5 w-5" />, label: 'Casas de banho', value: bathrooms })
  }
  if (areaM2 !== undefined) {
    facts.push({ icon: <Maximize2 className="h-5 w-5" />, label: 'Área útil', value: `${areaM2} m²` })
  }
  if (landAreaM2 !== undefined) {
    facts.push({ icon: <LandPlot className="h-5 w-5" />, label: 'Terreno', value: `${landAreaM2} m²` })
  }
  facts.push({
    icon: <Building2 className="h-5 w-5" />,
    label: 'Tipo',
    value: typeLabels[propertyType] || propertyType,
  })
  if (createdAt) {
    facts.push({
      icon: <Calendar className="h-5 w-5" />,
      label: 'Publicado',
      value: new Date(createdAt).toLocaleDateString('pt-PT'),
    })
  }

  if (facts.length === 0) return null

  return (
    <div className="mb-8">
      <h2 className="text-lg font-semibold text-gray-900 mb-4">Factos</h2>
      <div
        className={cn(
          'grid gap-3',
          facts.length <= 3
            ? 'grid-cols-1 sm:grid-cols-3'
            : 'grid-cols-2 sm:grid-cols-3 lg:grid-cols-6',
        )}
      >
        {facts.map((fact, i) => (
          <div
            key={i}
            className="flex items-start gap-3 p-4 bg-gray-50 rounded-lg border border-gray-100"
          >
            <div className="text-gray-400 shrink-0">{fact.icon}</div>
            <div className="min-w-0">
              <p className="text-xs text-gray-500">{fact.label}</p>
              <p className="text-sm font-semibold text-gray-900 truncate">
                {fact.value}
              </p>
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
