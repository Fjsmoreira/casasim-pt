import { Heart, House, MapPinned, UserRound } from 'lucide-react'
import { Outlet, Link } from 'react-router-dom'

export default function Layout() {
  return (
    <div className="min-h-screen flex flex-col">
      <header className="border-b border-sky-600 bg-[#4f8fb3] text-white">
        <nav className="mx-auto flex h-14 max-w-7xl items-center justify-between px-4 sm:px-6 lg:px-8">
          <Link to="/" className="rounded bg-[#1e3a5f] px-2.5 py-1 text-lg font-bold italic tracking-tight shadow-sm">
            CasaSim.pt
          </Link>
          <div className="hidden items-center gap-6 text-sm font-medium md:flex">
            <Link to="/search?transaction=sale" className="transition-opacity hover:opacity-75">Comprar</Link>
            <Link to="/search?transaction=rent" className="transition-opacity hover:opacity-75">Arrendar</Link>
            <Link to="/" className="transition-opacity hover:opacity-75">Vender</Link>
          </div>
          <div className="flex items-center gap-4 text-sm font-medium">
            <Link to="/" aria-label="Início" className="hidden items-center gap-1.5 sm:flex"><House className="size-4" />Início</Link>
            <Link to="/map" aria-label="Mapa" className="hidden items-center gap-1.5 sm:flex"><MapPinned className="size-4" />Mapa</Link>
            <Link to="/" aria-label="Favoritos" className="flex items-center gap-1.5"><Heart className="size-4" /><span className="hidden sm:inline">Favoritos</span></Link>
            <Link to="/admin" aria-label="Conta" className="flex items-center gap-1.5"><UserRound className="size-4" /><span className="hidden sm:inline">Conta</span></Link>
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
