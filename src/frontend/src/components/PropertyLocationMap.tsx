import { MapContainer, TileLayer, Marker, Popup } from 'react-leaflet'
import { MapPin } from 'lucide-react'
import { cn } from '@/lib/utils'

interface PropertyLocationMapProps {
  latitude: number
  longitude: number
  title: string
  price?: string
}

export default function PropertyLocationMap({
  latitude,
  longitude,
  title,
  price,
}: PropertyLocationMapProps) {
  return (
    <div className="mb-8">
      <h2 className="text-lg font-semibold text-gray-900 mb-3">Localização</h2>
      <div className="h-64 rounded-lg overflow-hidden border border-gray-200">
        <MapContainer
          center={[latitude, longitude]}
          zoom={15}
          className="h-full w-full"
          scrollWheelZoom={false}
          dragging={true}
        >
          <TileLayer
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          />
          <Marker position={[latitude, longitude]}>
            <Popup>
              <div className="text-sm">
                <p className="font-medium">{title}</p>
                {price && <p className="text-emerald-700 font-semibold">{price}</p>}
              </div>
            </Popup>
          </Marker>
        </MapContainer>
      </div>
    </div>
  )
}

/**
 * A small info card shown when coordinates are not available.
 */
export function LocationUnavailable() {
  return (
    <div className="mb-8">
      <h2 className="text-lg font-semibold text-gray-900 mb-3">Localização</h2>
      <div className={cn(
        'h-48 rounded-lg border border-dashed border-gray-300 bg-gray-50',
        'flex flex-col items-center justify-center text-gray-400 gap-2',
      )}>
        <MapPin className="h-8 w-8" />
        <p className="text-sm">Localização não disponível</p>
      </div>
    </div>
  )
}
