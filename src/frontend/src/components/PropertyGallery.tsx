import { useState } from 'react'
import { ChevronLeft, ChevronRight, X } from 'lucide-react'
import { cn } from '@/lib/utils'

interface PropertyGalleryProps {
  images: string[]
  title: string
}

export default function PropertyGallery({ images, title }: PropertyGalleryProps) {
  const [currentIndex, setCurrentIndex] = useState(0)
  const [fullscreen, setFullscreen] = useState(false)

  if (images.length === 0) return null

  const goTo = (index: number) => {
    setCurrentIndex(Math.max(0, Math.min(index, images.length - 1)))
  }

  const main = (
    <div className="relative group">
      <img
        src={images[currentIndex]}
        alt={`${title} — foto ${currentIndex + 1}`}
        className="w-full h-72 sm:h-96 object-cover rounded-xl cursor-pointer"
        onClick={() => setFullscreen(true)}
      />

      {/* Navigation arrows (only if more than one image) */}
      {images.length > 1 && (
        <>
          <button
            onClick={(e) => { e.stopPropagation(); goTo(currentIndex - 1) }}
            disabled={currentIndex === 0}
            className={cn(
              'absolute left-2 top-1/2 -translate-y-1/2 p-1.5 rounded-full bg-white/80 hover:bg-white shadow transition-opacity',
              currentIndex === 0 ? 'opacity-0 pointer-events-none' : 'opacity-0 group-hover:opacity-100'
            )}
            aria-label="Foto anterior"
          >
            <ChevronLeft className="h-5 w-5 text-gray-700" />
          </button>
          <button
            onClick={(e) => { e.stopPropagation(); goTo(currentIndex + 1) }}
            disabled={currentIndex === images.length - 1}
            className={cn(
              'absolute right-2 top-1/2 -translate-y-1/2 p-1.5 rounded-full bg-white/80 hover:bg-white shadow transition-opacity',
              currentIndex === images.length - 1 ? 'opacity-0 pointer-events-none' : 'opacity-0 group-hover:opacity-100'
            )}
            aria-label="Próxima foto"
          >
            <ChevronRight className="h-5 w-5 text-gray-700" />
          </button>

          {/* Counter badge */}
          <span className="absolute bottom-3 right-3 bg-black/60 text-white text-xs px-2 py-1 rounded-full">
            {currentIndex + 1} / {images.length}
          </span>
        </>
      )}
    </div>
  )

  return (
    <>
      {main}

      {/* Thumbnails strip */}
      {images.length > 1 && (
        <div className="flex gap-2 mt-2 overflow-x-auto pb-1">
          {images.map((img, i) => (
            <button
              key={i}
              onClick={() => goTo(i)}
              className={cn(
                'shrink-0 w-16 h-12 rounded-md overflow-hidden border-2 transition-all',
                i === currentIndex
                  ? 'border-emerald-500 ring-1 ring-emerald-400'
                  : 'border-transparent opacity-70 hover:opacity-100'
              )}
            >
              <img
                src={img}
                alt={`${title} — miniatura ${i + 1}`}
                className="w-full h-full object-cover"
              />
            </button>
          ))}
        </div>
      )}

      {/* Fullscreen overlay */}
      {fullscreen && (
        <div
          className="fixed inset-0 z-50 bg-black/90 flex items-center justify-center"
          onClick={() => setFullscreen(false)}
        >
          <button
            onClick={() => setFullscreen(false)}
            className="absolute top-4 right-4 p-2 rounded-full bg-white/20 hover:bg-white/30 text-white transition-colors"
            aria-label="Fechar"
          >
            <X className="h-6 w-6" />
          </button>

          <img
            src={images[currentIndex]}
            alt={`${title} — foto ${currentIndex + 1}`}
            className="max-w-[90vw] max-h-[90vh] object-contain"
          />

          {images.length > 1 && (
            <>
              <button
                onClick={(e) => { e.stopPropagation(); goTo(currentIndex - 1) }}
                disabled={currentIndex === 0}
                className="absolute left-4 top-1/2 -translate-y-1/2 p-2 rounded-full bg-white/20 hover:bg-white/30 text-white disabled:opacity-30 transition-colors"
                aria-label="Foto anterior"
              >
                <ChevronLeft className="h-8 w-8" />
              </button>
              <button
                onClick={(e) => { e.stopPropagation(); goTo(currentIndex + 1) }}
                disabled={currentIndex === images.length - 1}
                className="absolute right-4 top-1/2 -translate-y-1/2 p-2 rounded-full bg-white/20 hover:bg-white/30 text-white disabled:opacity-30 transition-colors"
                aria-label="Próxima foto"
              >
                <ChevronRight className="h-8 w-8" />
              </button>

              <span className="absolute bottom-6 left-1/2 -translate-x-1/2 bg-black/60 text-white text-sm px-3 py-1.5 rounded-full">
                {currentIndex + 1} / {images.length}
              </span>
            </>
          )}
        </div>
      )}
    </>
  )
}
