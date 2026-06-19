import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import axios from 'axios'
import apiClient from '../lib/api'
import type { AdminListingsResponse, AdminAgency } from '../types/api'
import AdminScraperPanel from '../components/AdminScraperPanel'

interface DashboardData {
  totalListings: number
  activeListings: number
  scrapedToday: number
}

type AdminTab = 'dashboard' | 'listings' | 'scrapers'

const LISTING_STATUSES = ['Active', 'Reserved', 'Pending', 'Sold', 'Rented', 'Removed', 'Archived']

function formatDate(iso: string): string {
  const d = new Date(iso)
  return d.toLocaleDateString('pt-PT', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function statusBadgeClass(status: string): string {
  switch (status) {
    case 'Active':
      return 'bg-emerald-100 text-emerald-800'
    case 'Reserved':
      return 'bg-amber-100 text-amber-800'
    case 'Pending':
      return 'bg-blue-100 text-blue-800'
    case 'Sold':
    case 'Rented':
      return 'bg-slate-100 text-slate-600'
    case 'Removed':
    case 'Archived':
      return 'bg-red-100 text-red-700'
    default:
      return 'bg-gray-100 text-gray-600'
  }
}

export default function AdminPage() {
  const navigate = useNavigate()
  const [apiKey, setApiKey] = useState(() => sessionStorage.getItem('casasim-admin-key') ?? '')
  const [inputKey, setInputKey] = useState('')
  const [dashboard, setDashboard] = useState<DashboardData | null>(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  // Tab state
  const [activeTab, setActiveTab] = useState<AdminTab>('dashboard')

  // Listings table state
  const [listings, setListings] = useState<AdminListingsResponse | null>(null)
  const [listingsLoading, setListingsLoading] = useState(false)
  const [listingsError, setListingsError] = useState('')
  const [page, setPage] = useState(1)
  const [filterStatus, setFilterStatus] = useState('')
  const [filterAgency, setFilterAgency] = useState('')
  const [filterSearch, setFilterSearch] = useState('')
  const [searchInput, setSearchInput] = useState('')
  const [agencies, setAgencies] = useState<AdminAgency[]>([])

  const isAuthenticated = !!apiKey

  const fetchDashboard = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const res = await apiClient.get<DashboardData>('/admin/dashboard')
      setDashboard(res.data)
    } catch (err: unknown) {
      if (axios.isAxiosError(err) && err.response?.status === 401) {
        sessionStorage.removeItem('casasim-admin-key')
        setApiKey('')
        setError('Chave de API inválida.')
      } else {
        setError('Erro ao carregar painel de administração.')
      }
    } finally {
      setLoading(false)
    }
  }, [])

  const fetchListings = useCallback(async () => {
    setListingsLoading(true)
    setListingsError('')
    try {
      const params = new URLSearchParams()
      params.set('page', String(page))
      params.set('pageSize', '25')
      if (filterStatus) params.set('status', filterStatus)
      if (filterAgency) params.set('agency', filterAgency)
      if (filterSearch) params.set('search', filterSearch)

      const res = await apiClient.get<AdminListingsResponse>(`/admin/listings?${params.toString()}`)
      setListings(res.data)
    } catch (err: unknown) {
      if (axios.isAxiosError(err) && err.response?.status === 401) {
        sessionStorage.removeItem('casasim-admin-key')
        setApiKey('')
        setListingsError('Sessão expirada. Faça login novamente.')
      } else {
        setListingsError('Erro ao carregar listagens.')
      }
    } finally {
      setListingsLoading(false)
    }
  }, [page, filterStatus, filterAgency, filterSearch])

  const fetchAgencies = useCallback(async () => {
    try {
      const res = await apiClient.get<AdminAgency[]>('/admin/agencies')
      setAgencies(res.data)
    } catch {
      // Non-critical — filters still work without dropdown
    }
  }, [])

  useEffect(() => {
    if (isAuthenticated) {
      fetchDashboard()
      fetchAgencies()
    }
  }, [isAuthenticated, fetchDashboard, fetchAgencies])

  useEffect(() => {
    if (isAuthenticated && activeTab === 'listings') {
      fetchListings()
    }
  }, [isAuthenticated, activeTab, fetchListings])

  function handleLogin(e: React.FormEvent) {
    e.preventDefault()
    const trimmed = inputKey.trim()
    if (!trimmed) return
    sessionStorage.setItem('casasim-admin-key', trimmed)
    setApiKey(trimmed)
    setInputKey('')
  }

  function handleLogout() {
    sessionStorage.removeItem('casasim-admin-key')
    setApiKey('')
    setDashboard(null)
    setListings(null)
    setError('')
  }

  function handleSearchSubmit(e: React.FormEvent) {
    e.preventDefault()
    setFilterSearch(searchInput.trim())
    setPage(1)
  }

  function handleFilterChange(fn: (val: string) => void, val: string) {
    fn(val)
    setPage(1)
  }

  // ── Login prompt ──
  if (!isAuthenticated) {
    return (
      <div className="max-w-sm mx-auto px-4 py-16">
        <h1 className="text-2xl font-bold text-gray-900 mb-6 text-center">
          Administração
        </h1>
        <form onSubmit={handleLogin} className="space-y-4">
          <div>
            <label htmlFor="api-key" className="block text-sm font-medium text-gray-700 mb-1">
              Chave de API
            </label>
            <input
              id="api-key"
              type="password"
              value={inputKey}
              onChange={(e) => setInputKey(e.target.value)}
              placeholder="Insira a chave de administração"
              className="w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500"
              autoFocus
            />
          </div>
          {error && (
            <p className="text-sm text-red-600">{error}</p>
          )}
          <button
            type="submit"
            className="w-full bg-emerald-700 text-white py-2 px-4 rounded-md hover:bg-emerald-800 transition-colors font-medium"
          >
            Entrar
          </button>
        </form>
        <p className="text-xs text-gray-400 text-center mt-6">
          Apenas utilizadores autorizados.
        </p>
      </div>
    )
  }

  // ── Admin UI ──
  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Administração</h1>
        <button
          onClick={handleLogout}
          className="text-sm text-gray-500 hover:text-gray-700 underline"
        >
          Sair
        </button>
      </div>

      {/* Tabs */}
      <div className="border-b border-gray-200 mb-6">
        <nav className="-mb-px flex space-x-6">
          <button
            onClick={() => setActiveTab('dashboard')}
            className={`pb-3 text-sm font-medium border-b-2 transition-colors ${
              activeTab === 'dashboard'
                ? 'border-emerald-700 text-emerald-700'
                : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
            }`}
          >
            Painel
          </button>
          <button
            onClick={() => setActiveTab('listings')}
            className={`pb-3 text-sm font-medium border-b-2 transition-colors ${
              activeTab === 'listings'
                ? 'border-emerald-700 text-emerald-700'
                : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
            }`}
          >
            Listagens
          </button>
          <button
            onClick={() => setActiveTab('scrapers')}
            className={`pb-3 text-sm font-medium border-b-2 transition-colors ${
              activeTab === 'scrapers'
                ? 'border-emerald-700 text-emerald-700'
                : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
            }`}
          >
            Scrapers
          </button>
        </nav>
      </div>

      {/* Dashboard tab */}
      {activeTab === 'dashboard' && (
        <>
          {loading && (
            <p className="text-gray-500">A carregar painel…</p>
          )}

          {error && (
            <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-md mb-6">
              {error}
              <button
                onClick={fetchDashboard}
                className="ml-3 underline text-sm"
              >
                Tentar novamente
              </button>
            </div>
          )}

          {dashboard && (
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-6">
              <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
                <p className="text-sm text-gray-500 mb-1">Total de Imóveis</p>
                <p className="text-3xl font-bold text-gray-900">{dashboard.totalListings}</p>
              </div>
              <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
                <p className="text-sm text-gray-500 mb-1">Imóveis Activos</p>
                <p className="text-3xl font-bold text-emerald-700">{dashboard.activeListings}</p>
              </div>
              <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
                <p className="text-sm text-gray-500 mb-1">Scraped Hoje</p>
                <p className="text-3xl font-bold text-blue-700">{dashboard.scrapedToday}</p>
              </div>
            </div>
          )}
        </>
      )}

      {/* Listings tab */}
      {activeTab === 'listings' && (
        <div>
          {/* Filters bar */}
          <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-4 mb-4">
            <div className="flex flex-wrap items-end gap-3">
              {/* Status filter */}
              <div className="flex-1 min-w-[140px]">
                <label className="block text-xs font-medium text-gray-500 mb-1">Estado</label>
                <select
                  value={filterStatus}
                  onChange={(e) => handleFilterChange(setFilterStatus, e.target.value)}
                  className="w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                >
                  <option value="">Todos</option>
                  {LISTING_STATUSES.map((s) => (
                    <option key={s} value={s}>{s}</option>
                  ))}
                </select>
              </div>

              {/* Agency filter */}
              <div className="flex-1 min-w-[160px]">
                <label className="block text-xs font-medium text-gray-500 mb-1">Fonte</label>
                <select
                  value={filterAgency}
                  onChange={(e) => handleFilterChange(setFilterAgency, e.target.value)}
                  className="w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                >
                  <option value="">Todas</option>
                  {agencies.map((a) => (
                    <option key={a.slug} value={a.slug}>{a.name}</option>
                  ))}
                </select>
              </div>

              {/* Search */}
              <form onSubmit={handleSearchSubmit} className="flex-1 min-w-[200px]">
                <label className="block text-xs font-medium text-gray-500 mb-1">Pesquisar</label>
                <div className="flex gap-2">
                  <input
                    type="text"
                    value={searchInput}
                    onChange={(e) => setSearchInput(e.target.value)}
                    placeholder="Título ou cidade…"
                    className="flex-1 border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                  />
                  <button
                    type="submit"
                    className="bg-emerald-700 text-white px-3 py-1.5 rounded-md text-sm hover:bg-emerald-800 transition-colors"
                  >
                    Filtrar
                  </button>
                </div>
              </form>

              {/* Clear filters */}
              {(filterStatus || filterAgency || filterSearch) && (
                <button
                  onClick={() => {
                    setFilterStatus('')
                    setFilterAgency('')
                    setFilterSearch('')
                    setSearchInput('')
                    setPage(1)
                  }}
                  className="text-sm text-gray-500 hover:text-gray-700 underline whitespace-nowrap pb-1"
                >
                  Limpar filtros
                </button>
              )}
            </div>
          </div>

          {/* Error */}
          {listingsError && (
            <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-md mb-4">
              {listingsError}
            </div>
          )}

          {/* Loading */}
          {listingsLoading && (
            <p className="text-gray-500 mb-4">A carregar listagens…</p>
          )}

          {/* Table */}
          {listings && (
            <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-gray-200">
                  <thead className="bg-gray-50">
                    <tr>
                      <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider w-16">
                        Imagem
                      </th>
                      <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Título
                      </th>
                      <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider w-28">
                        Preço
                      </th>
                      <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider w-28">
                        Fonte
                      </th>
                      <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider w-24">
                        Estado
                      </th>
                      <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider w-36">
                        Última vez
                      </th>
                    </tr>
                  </thead>
                  <tbody className="bg-white divide-y divide-gray-200">
                    {listings.items.length === 0 ? (
                      <tr>
                        <td colSpan={6} className="px-4 py-12 text-center text-gray-500 text-sm">
                          Nenhuma listagem encontrada.
                        </td>
                      </tr>
                    ) : (
                      listings.items.map((item) => (
                        <tr
                          key={item.id}
                          className="hover:bg-gray-50 cursor-pointer transition-colors"
                          onClick={() => navigate(`/listings/${item.id}`)}
                        >
                          {/* Thumbnail */}
                          <td className="px-4 py-3 whitespace-nowrap">
                            <div className="w-12 h-12 rounded-md overflow-hidden bg-gray-100 flex-shrink-0">
                              {item.thumbnailUrl ? (
                                <img
                                  src={item.thumbnailUrl}
                                  alt=""
                                  className="w-full h-full object-cover"
                                  onError={(e) => {
                                    (e.target as HTMLImageElement).style.display = 'none'
                                  }}
                                />
                              ) : (
                                <div className="w-full h-full flex items-center justify-center text-gray-300">
                                  <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z" />
                                  </svg>
                                </div>
                              )}
                            </div>
                          </td>

                          {/* Title */}
                          <td className="px-4 py-3">
                            <div className="text-sm font-medium text-gray-900 truncate max-w-xs">
                              {item.title}
                            </div>
                            <div className="text-xs text-gray-500 mt-0.5">
                              {item.city && <span>{item.city}</span>}
                              {item.bedrooms != null && (
                                <span className="ml-2">{item.bedrooms} quartos</span>
                              )}
                              {item.areaM2 != null && (
                                <span className="ml-2">{item.areaM2}m²</span>
                              )}
                            </div>
                          </td>

                          {/* Price */}
                          <td className="px-4 py-3 whitespace-nowrap text-right">
                            <span className="text-sm font-semibold text-gray-900">
                              {item.priceFormatted ?? '—'}
                            </span>
                          </td>

                          {/* Source */}
                          <td className="px-4 py-3 whitespace-nowrap">
                            <span className="text-sm text-gray-700">
                              {item.agencyName ?? '—'}
                            </span>
                          </td>

                          {/* Status */}
                          <td className="px-4 py-3 whitespace-nowrap">
                            <span
                              className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${statusBadgeClass(item.status)}`}
                            >
                              {item.status}
                            </span>
                          </td>

                          {/* Last seen */}
                          <td className="px-4 py-3 whitespace-nowrap text-right text-sm text-gray-500">
                            {item.lastSeenAt ? formatDate(item.lastSeenAt) : '—'}
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>

              {/* Pagination */}
              {listings.totalPages > 1 && (
                <div className="flex items-center justify-between px-4 py-3 border-t border-gray-200 bg-gray-50">
                  <span className="text-sm text-gray-500">
                    Página {listings.page} de {listings.totalPages} ({listings.totalCount} total)
                  </span>
                  <div className="flex gap-2">
                    <button
                      disabled={page <= 1}
                      onClick={() => setPage((p) => Math.max(1, p - 1))}
                      className="px-3 py-1 text-sm border border-gray-300 rounded-md hover:bg-gray-100 disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                      Anterior
                    </button>
                    <button
                      disabled={page >= listings.totalPages}
                      onClick={() => setPage((p) => p + 1)}
                      className="px-3 py-1 text-sm border border-gray-300 rounded-md hover:bg-gray-100 disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                      Seguinte
                    </button>
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      )}

      {activeTab === 'scrapers' && (
        <AdminScraperPanel
          onAuthExpired={() => {
            sessionStorage.removeItem('casasim-admin-key')
            setApiKey('')
            setError('Sessão expirada. Faça login novamente.')
          }}
        />
      )}
    </div>
  )
}
