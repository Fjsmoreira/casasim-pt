import { MapContainer, TileLayer } from 'react-leaflet'

export default function MapPage() {
  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <h1 className="text-2xl font-bold text-gray-900 mb-4">Mapa</h1>
      <p className="text-gray-500 mb-6">
        Explore os imóveis no mapa interativo.
      </p>
      <div className="h-[600px] rounded-lg overflow-hidden border border-gray-200">
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
        </MapContainer>
      </div>
    </div>
  )
}
