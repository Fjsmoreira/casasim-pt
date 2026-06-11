import { useEffect, useState } from 'react'
import apiClient from '../lib/api'
import type { ScraperStatusResponse } from '../types/api'

interface ScraperStatusPanelProps {
  /** Called after a successful fetch (e.g., to clear parent errors) */
  onFetchSuccess?: () => void
  /** Called when an error occurs (e.g., 401 forwarded to parent) */
  onError?: (msg: string) => void
}

export default function ScraperStatusPanel({ onFetchSuccess, onError }: ScraperStatusPanelProps) {
  const [data, setData] = useState<ScraperStatusResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [running, setRunning] = useState(false)

  async function fetchStatus() {
    setLoading(true)
    try {
      const res = await apiClient.get<ScraperStatusResponse>('/admin/scraper-status')
      setData(res.data)
      onFetchSuccess?.()
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Erro ao carregar estado dos scrapers.'
      onError?.(msg)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchStatus()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  async function handleRunNow() {
    setRunning(true)
    try {
      // Placeholder: POST /api/admin/scraper/run trigger
      await apiClient.post('/admin/scraper/run')
      // Refetch after brief delay to pick up the new Started entry
      setTimeout(fetchStatus, 1500)
    } catch {
      onError?.('Erro ao iniciar scraper.')
    } finally {
      setRunning(false)
    }
  }

  // ── Status badge ─────────────────────────────────────────────
  function StatusBadge({ status }: { status: string }) {
    const colors: Record<string, string> = {
      Succeeded: 'bg-green-100 text-green-800',
      PartiallySucceeded: 'bg-yellow-100 text-yellow-800',
      Failed: 'bg-red-100 text-red-800',
      Started: 'bg-blue-100 text-blue-800',
      Cancelled: 'bg-gray-100 text-gray-500',
    }
    const labels: Record<string, string> = {
      Succeeded: 'Sucesso',
      PartiallySucceeded: 'Parcial',
      Failed: 'Falhou',
      Started: 'Em curso',
      Cancelled: 'Cancelado',
    }
    return (
      <span
        className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${
          colors[status] ?? 'bg-gray-100 text-gray-600'
        }`}
      >
        {labels[status] ?? status}
      </span>
    )
  }

  // ── Format helpers ───────────────────────────────────────────
  function fmtDate(d: string | null) {
    if (!d) return '—'
    const dt = new Date(d)
    return dt.toLocaleString('pt-PT', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    })
  }

  function fmtRelative(d: string | null) {
    if (!d) return ''
    const diff = Date.now() - new Date(d).getTime()
    const mins = Math.floor(diff / 60_000)
    if (mins < 1) return 'agora'
    if (mins < 60) return `há ${mins} min`
    const hours = Math.floor(mins / 60)
    if (hours < 24) return `há ${hours}h`
    const days = Math.floor(hours / 24)
    return `há ${days}d`
  }

  // ── Loading state ────────────────────────────────────────────
  if (loading) {
    return (
      <div className="mt-8">
        <h2 className="text-lg font-semibold text-gray-900 mb-4">Estado dos Scrapers</h2>
        <p className="text-gray-500 text-sm">A carregar estado dos scrapers…</p>
      </div>
    )
  }

  // ── Empty state ──────────────────────────────────────────────
  if (!data || data.sources.length === 0) {
    return (
      <div className="mt-8">
        <SectionHeader onRunNow={handleRunNow} running={running} onRefresh={fetchStatus} />
        <div className="bg-white rounded-lg border border-gray-200 p-8 text-center">
          <p className="text-gray-500">Nenhuma execução de scraper registada ainda.</p>
          <p className="text-gray-400 text-sm mt-1">
            As execuções aparecerão aqui assim que o scraper for iniciado.
          </p>
        </div>
      </div>
    )
  }

  return (
    <div className="mt-8 space-y-6">
      <SectionHeader onRunNow={handleRunNow} running={running} onRefresh={fetchStatus} />

      {/* ── Last run summary ─────────────────────────────────── */}
      {data.lastRunOverall && (
        <p className="text-xs text-gray-400">
          Última execução: {fmtDate(data.lastRunOverall)} ({fmtRelative(data.lastRunOverall)})
        </p>
      )}

      {/* ── Source run cards ─────────────────────────────────── */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        {data.sources.map((src) => (
          <div
            key={src.sourceName}
            className="bg-white rounded-lg shadow-sm border border-gray-200 p-4"
          >
            <div className="flex items-center justify-between mb-3">
              <h3 className="font-medium text-gray-900">{src.sourceName}</h3>
              <StatusBadge status={src.status} />
            </div>

            <div className="space-y-1 text-sm text-gray-600">
              <div className="flex justify-between">
                <span>Início</span>
                <span className="text-gray-900">{fmtDate(src.startedAt)}</span>
              </div>
              <div className="flex justify-between">
                <span>Fim</span>
                <span className="text-gray-900">{fmtDate(src.completedAt)}</span>
              </div>
            </div>

            <div className="mt-3 grid grid-cols-2 gap-2 text-xs">
              <div className="bg-gray-50 rounded p-2 text-center">
                <p className="font-semibold text-gray-900">{src.listingsFound}</p>
                <p className="text-gray-500">Encontrados</p>
              </div>
              <div className="bg-gray-50 rounded p-2 text-center">
                <p className="font-semibold text-gray-900">{src.listingsCreated}</p>
                <p className="text-gray-500">Criados</p>
              </div>
              <div className="bg-gray-50 rounded p-2 text-center">
                <p className="font-semibold text-gray-900">{src.listingsUpdated}</p>
                <p className="text-gray-500">Actualizados</p>
              </div>
              <div className="bg-gray-50 rounded p-2 text-center">
                <p className="font-semibold text-gray-900">{src.listingsRemoved}</p>
                <p className="text-gray-500">Removidos</p>
              </div>
            </div>

            {src.errorMessage && (
              <p className="mt-2 text-xs text-red-600 bg-red-50 rounded p-2 leading-relaxed">
                {src.errorMessage}
              </p>
            )}
          </div>
        ))}
      </div>

      {/* ── Run counts summary ───────────────────────────────── */}
      <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-4">
        <h3 className="text-sm font-semibold text-gray-900 mb-2">Resumo de Execuções</h3>
        <div className="flex flex-wrap gap-3 text-sm">
          {Object.entries(data.runCounts).map(([status, count]) => (
            <span key={status} className="flex items-center gap-1.5">
              <StatusBadge status={status} />
              <span className="text-gray-700 font-medium">{count}</span>
            </span>
          ))}
        </div>
      </div>

      {/* ── Recent errors ────────────────────────────────────── */}
      {data.recentErrors.length > 0 && (
        <div className="bg-white rounded-lg shadow-sm border border-red-200 p-4">
          <h3 className="text-sm font-semibold text-red-700 mb-3">
            Erros Recentes ({data.recentErrors.length})
          </h3>
          <div className="space-y-2">
            {data.recentErrors.map((err, i) => (
              <div
                key={i}
                className="bg-red-50 border border-red-100 rounded p-3 text-xs"
              >
                <div className="flex items-center justify-between mb-1">
                  <span className="font-medium text-red-800">{err.sourceName}</span>
                  <span className="text-red-400">{fmtRelative(err.startedAt)}</span>
                </div>
                {err.errorMessage && (
                  <p className="text-red-700 leading-relaxed">{err.errorMessage}</p>
                )}
                {err.errorDetails && (
                  <pre className="mt-1 text-red-500 whitespace-pre-wrap overflow-x-auto max-h-20">
                    {err.errorDetails}
                  </pre>
                )}
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}

// ── Section header with Run Now button ────────────────────────

function SectionHeader({
  onRunNow,
  running,
  onRefresh,
}: {
  onRunNow: () => void
  running: boolean
  onRefresh: () => void
}) {
  return (
    <div className="flex items-center justify-between">
      <h2 className="text-lg font-semibold text-gray-900">Estado dos Scrapers</h2>
      <div className="flex items-center gap-2">
        <button
          onClick={onRefresh}
          className="text-xs text-gray-500 hover:text-gray-700 underline"
          disabled={running}
        >
          Actualizar
        </button>
        <button
          onClick={onRunNow}
          disabled={running}
          className="inline-flex items-center gap-1.5 bg-emerald-700 text-white text-sm font-medium px-4 py-2 rounded-md hover:bg-emerald-800 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {running ? (
            <>
              <svg
                className="animate-spin h-4 w-4"
                xmlns="http://www.w3.org/2000/svg"
                fill="none"
                viewBox="0 0 24 24"
              >
                <circle
                  className="opacity-25"
                  cx="12"
                  cy="12"
                  r="10"
                  stroke="currentColor"
                  strokeWidth="4"
                />
                <path
                  className="opacity-75"
                  fill="currentColor"
                  d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
                />
              </svg>
              A executar…
            </>
          ) : (
            <>
              <svg
                className="h-4 w-4"
                xmlns="http://www.w3.org/2000/svg"
                fill="none"
                viewBox="0 0 24 24"
                strokeWidth={2}
                stroke="currentColor"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  d="M5 3l14 9-14 9V3z"
                />
              </svg>
              Executar Agora
            </>
          )}
        </button>
      </div>
    </div>
  )
}
