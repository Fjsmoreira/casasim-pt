import { useState, useEffect, useCallback } from 'react'
import axios from 'axios'
import apiClient from '../lib/api'

interface DashboardData {
  totalListings: number
  activeListings: number
  scrapedToday: number
}

export default function AdminPage() {
  const [apiKey, setApiKey] = useState(() => sessionStorage.getItem('casasim-admin-key') ?? '')
  const [inputKey, setInputKey] = useState('')
  const [dashboard, setDashboard] = useState<DashboardData | null>(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

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

  useEffect(() => {
    if (isAuthenticated) {
      fetchDashboard()
    }
  }, [isAuthenticated, fetchDashboard])

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
    setError('')
  }

  // --- Login prompt ---
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

  // --- Admin dashboard ---
  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <div className="flex items-center justify-between mb-8">
        <h1 className="text-2xl font-bold text-gray-900">Administração</h1>
        <button
          onClick={handleLogout}
          className="text-sm text-gray-500 hover:text-gray-700 underline"
        >
          Sair
        </button>
      </div>

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
    </div>
  )
}
