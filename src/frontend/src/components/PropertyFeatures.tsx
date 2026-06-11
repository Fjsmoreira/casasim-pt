import { Sparkles } from 'lucide-react'

interface PropertyFeaturesProps {
  features: string[]
}

export default function PropertyFeatures({ features }: PropertyFeaturesProps) {
  if (!features || features.length === 0) return null

  return (
    <div className="mb-8">
      <h2 className="text-lg font-semibold text-gray-900 mb-3">Características</h2>
      <div className="flex flex-wrap gap-2">
        {features.map((feature, i) => (
          <span
            key={i}
            className="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 text-emerald-700 px-3 py-1.5 text-sm border border-emerald-200"
          >
            <Sparkles className="h-3.5 w-3.5" />
            {feature}
          </span>
        ))}
      </div>
    </div>
  )
}
