import { Link } from 'react-router-dom'

export default function HomePage() {
  return (
    <div>
      {/* Hero */}
      <section className="bg-gradient-to-br from-emerald-700 to-emerald-900 text-white">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-24 text-center">
          <h1 className="text-4xl sm:text-5xl font-bold mb-4">
            Encontre a sua casa em Pombal
          </h1>
          <p className="text-lg text-emerald-100 mb-8 max-w-2xl mx-auto">
            O maior agregador de imóveis da região de Pombal, Leiria. 
            Pesquise casas, apartamentos, terrenos e moradias de várias agências num só lugar.
          </p>
          <Link
            to="/search"
            className="inline-block bg-white text-emerald-800 font-semibold px-8 py-3 rounded-lg hover:bg-emerald-50 transition-colors"
          >
            Ver Imóveis
          </Link>
        </div>
      </section>

      {/* Stats */}
      <section className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-16">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-8 text-center">
          <div className="p-6 bg-white rounded-xl shadow-sm border border-gray-100">
            <div className="text-3xl font-bold text-emerald-600 mb-2">100+</div>
            <div className="text-gray-600">Imóveis listados</div>
          </div>
          <div className="p-6 bg-white rounded-xl shadow-sm border border-gray-100">
            <div className="text-3xl font-bold text-emerald-600 mb-2">5+</div>
            <div className="text-gray-600">Agências parceiras</div>
          </div>
          <div className="p-6 bg-white rounded-xl shadow-sm border border-gray-100">
            <div className="text-3xl font-bold text-emerald-600 mb-2">Pombal</div>
            <div className="text-gray-600">Região focada</div>
          </div>
        </div>
      </section>
    </div>
  )
}
