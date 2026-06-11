import { MapContainer, TileLayer, GeoJSON, useMap } from 'react-leaflet'
import L from 'leaflet'
import { useMapListings } from '@/hooks/useMapListings'
import type { MapPropertiesResponse } from '@/types/api'
import { Loader2 } from 'lucide-react'

// ── Default marker icon fix for Leaflet + bundlers ──
// Leaflet's default icon assets fail in bundlers because the image paths
// are relative. We override the default with a clean SVG-based icon.
const DefaultIcon = L.icon({
  iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  iconRetinaUrl:
    'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  shadowUrl:
    'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
  iconSize: [25, 41],
  iconAnchor: [12, 41],
  popupAnchor: [1, -34],
  shadowSize: [41, 41],
})

/** Format a price value in EUR */
function formatPrice(price: number): string {
  return new Intl.NumberFormat('pt-PT', {
    style: 'currency',
    currency: 'EUR',
    maximumFractionDigits: 0,
  }).format(price)
}

/** Build a human-readable title from GeoJSON properties */
function buildTitle(p: MapPropertiesResponse['features'][number]['properties']): string {
  const type = p.property_type === 'House' ? 'Moradia' : p.property_type === 'Apartment' ? 'Apartamento' : p.property_type === 'Land' ? 'Terreno' : p.property_type
  const location = p.city ?? 'Portugal'
  const beds = p.bedrooms ? ` - ${p.bedrooms} quartos` : ''
  return `${type} em ${location}${beds}`
}

/** Build the popup HTML content */
function popupContent(p: MapPropertiesResponse['features'][number]['properties']): string {
  const thumb = p.thumbnail
    ? `<img src="${p.thumbnail}" alt="${p.id}" style="width:160px;height:120px;object-fit:cover;border-radius:6px;margin-bottom:6px;" />`
    : `<div style="width:160px;height:120px;background:#e5e7eb;border-radius:6px;margin-bottom:6px;display:flex;align-items:center;justify-content:center;color:#6b7280;font-size:12px;">Sem foto</div>`

  const price = p.price != null
    ? `<div style="font-size:15px;font-weight:700;color:#059669;">${formatPrice(p.price)}</div>`
    : ''

  const title = buildTitle(p)

  return `
    <div style="font-family:system-ui,sans-serif;width:160px;">
      ${thumb}
      ${price}
      <div style="font-size:13px;color:#374151;margin:2px 0 6px;">${title}</div>
      <a href="/listings/${p.id}" style="font-size:12px;color:#2563eb;text-decoration:underline;">Ver detalhes →</a>
    </div>
  `
}

// ── Inner component: uses the map instance via useMap() ──
function MapContent() {
  const { data, isLoading, isError } = useMapListings()
  const map = useMap()

  if (isLoading) {
    return (
      <div className="absolute inset-0 flex items-center justify-center bg-white/80 z-[1000]">
        <Loader2 className="h-8 w-8 animate-spin text-emerald-600" />
        <span className="ml-2 text-gray-600">A carregar imóveis...</span>
      </div>
    )
  }

  if (isError) {
    return (
      <div className="absolute inset-0 flex items-center justify-center bg-white/80 z-[1000]">
        <span className="text-red-600">Erro ao carregar imóveis no mapa.</span>
      </div>
    )
  }

  if (!data || data.features.length === 0) {
    return (
      <div className="absolute inset-0 flex items-center justify-center bg-white/80 z-[1000]">
        <span className="text-gray-500">Nenhum imóvel encontrado nesta área.</span>
      </div>
    )
  }

  // Fit map bounds to the markers
  const bounds = L.geoJSON(data as unknown as GeoJSON.GeoJsonObject).getBounds()
  if (bounds.isValid()) {
    map.fitBounds(bounds, { padding: [50, 50] })
  }

  return (
    <GeoJSON
      key={data.features.length}
      data={data as unknown as GeoJSON.GeoJsonObject}
      pointToLayer={(_feature, latlng) => L.marker(latlng, { icon: DefaultIcon })}
      onEachFeature={(feature, layer) => {
        if (feature.properties) {
          layer.bindPopup(popupContent(feature.properties as MapPropertiesResponse['features'][number]['properties']), {
            maxWidth: 200,
            className: 'property-popup',
          })
        }
      }}
    />
  )
}

// ── Page component ──
export default function MapPage() {
  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <h1 className="text-2xl font-bold text-gray-900 mb-4">Mapa</h1>
      <p className="text-gray-500 mb-6">
        Explore os imóveis no mapa interativo.
      </p>
      <div className="h-[600px] rounded-lg overflow-hidden border border-gray-200 relative">
        <MapContainer
          center={[39.915, -8.628]}
          zoom={12}
          className="h-full w-full"
          scrollWheelZoom={true}
        >
          <TileLayer
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          />
          <MapContent />
        </MapContainer>
      </div>
    </div>
  )
}
