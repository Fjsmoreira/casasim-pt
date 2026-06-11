import { useState } from 'react'
import { ChevronDown, ChevronUp } from 'lucide-react'
import { cn } from '@/lib/utils'

interface PropertyDescriptionProps {
  description: string
}

const MAX_CHARS = 500

export default function PropertyDescription({ description }: PropertyDescriptionProps) {
  const [expanded, setExpanded] = useState(false)
  const isLong = description.length > MAX_CHARS
  const displayText = isLong && !expanded ? description.slice(0, MAX_CHARS) : description

  return (
    <div className="mb-8">
      <h2 className="text-lg font-semibold text-gray-900 mb-3">Descrição</h2>
      <div className="relative">
        <p className="text-gray-600 leading-relaxed whitespace-pre-line">
          {displayText}
          {isLong && !expanded && (
            <>
              <span className="text-gray-300">…</span>
              <button
                onClick={() => setExpanded(true)}
                className="inline-flex items-center gap-1 ml-1 text-emerald-600 hover:text-emerald-700 text-sm font-medium transition-colors"
              >
                Ler mais
                <ChevronDown className="h-4 w-4" />
              </button>
            </>
          )}
        </p>
        {isLong && expanded && (
          <button
            onClick={() => setExpanded(false)}
            className={cn(
              'inline-flex items-center gap-1 mt-2 text-emerald-600 hover:text-emerald-700 text-sm font-medium transition-colors',
            )}
          >
            Ler menos
            <ChevronUp className="h-4 w-4" />
          </button>
        )}
      </div>
    </div>
  )
}
