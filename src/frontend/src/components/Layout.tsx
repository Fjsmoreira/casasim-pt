import { Outlet, Link } from 'react-router-dom'

export default function Layout() {
  return (
    <div className="min-h-screen flex flex-col">
      <header className="border-b border-gray-200 bg-white">
        <nav className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
          <Link to="/" className="text-xl font-bold text-emerald-700">
            CasaSim.pt
          </Link>
          <div className="flex gap-6 text-sm font-medium text-gray-600">
            <Link to="/" className="hover:text-emerald-700 transition-colors">
              Início
            </Link>
            <Link to="/search" className="hover:text-emerald-700 transition-colors">
              Imóveis
            </Link>
          </div>
        </nav>
      </header>
      <main className="flex-1">
        <Outlet />
      </main>
      <footer className="border-t border-gray-200 bg-gray-50 py-8">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 text-center text-sm text-gray-500">
          <p>&copy; {new Date().getFullYear()} CasaSim.pt — Propriedades em Pombal, Portugal</p>
        </div>
      </footer>
    </div>
  )
}
