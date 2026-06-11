import { useState, useEffect } from 'react'
import { MapContainer, TileLayer } from 'react-leaflet'
import { Link } from 'react-router-dom'
import L from 'leaflet'

// Fix default marker icons in webpack/vite builds
import markerIcon2x from 'leaflet/dist/images/marker-icon-2x.png'
import markerIcon from 'leaflet/dist/images/marker-icon.png'
import markerShadow from 'leaflet/dist/images/marker-shadow.png'

delete (L.Icon.Default.prototype as any)._getIconUrl
L.Icon.Default.mergeOptions({
  iconRetinaUrl: markerIcon2x,
  iconUrl: markerIcon,
  shadowUrl: markerShadow,
})

interface Property {
  id: string
  title: string
  price: number
  type: string
  transaction: string
  bedrooms?: number
  areaM2?: number
  city?: string
  images: string[]
  listingUrl?: string
}

export default function SearchPage() {
  const [properties, setProperties] = useState<Property[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    fetch('/api/properties')
      .then(res => res.json())
      .then(data => setProperties(data.items || []))
      .catch(() => setProperties([]))
      .finally(() => setLoading(false))
  }, [])

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <h1 className="text-2xl font-bold text-gray-900 mb-6">Imóveis em Pombal</h1>

      {loading ? (
        <p className="text-gray-500">A carregar...</p>
      ) : properties.length === 0 ? (
        <div>
          <p className="text-gray-500 mb-4">
            Nenhum imóvel encontrado. Os dados serão carregados à medida que o scraping for executado.
          </p>
          <div className="h-[400px] rounded-lg overflow-hidden border border-gray-200">
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
      ) : (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          <div className="lg:col-span-2 space-y-4">
            {properties.map(p => (
              <Link
                key={p.id}
                to={`/property/${p.id}`}
                className="block p-4 bg-white rounded-lg border border-gray-200 hover:border-emerald-300 transition-colors"
              >
                <div className="flex gap-4">
                  {p.images?.[0] && (
                    <img
                      src={p.images[0]}
                      alt={p.title}
                      className="w-32 h-24 object-cover rounded-lg"
                    />
                  )}
                  <div>
                    <h3 className="font-semibold text-gray-900">{p.title}</h3>
                    <p className="text-emerald-700 font-bold text-lg">
                      €{p.price.toLocaleString('pt-PT')}
                    </p>
                    <p className="text-sm text-gray-500">
                      {p.bedrooms && `${p.bedrooms} quartos · `}
                      {p.areaM2 && `${p.areaM2}m² · `}
                      {p.city || 'Pombal'}
                    </p>
                  </div>
                </div>
              </Link>
            ))}
          </div>
          <div className="h-[500px] rounded-lg overflow-hidden border border-gray-200 lg:sticky lg:top-8">
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
      )}
    </div>
  )
}
