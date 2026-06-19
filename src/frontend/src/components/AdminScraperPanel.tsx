import { useCallback, useEffect, useMemo, useState } from 'react'
import axios from 'axios'
import apiClient from '../lib/api'
import type {
  AdminScraperSource,
  ScrapeListingChange,
  ScrapeListingChangesResponse,
  ScraperRunDetail,
  ScraperRunsResponse,
  ScraperRunSummary,
} from '../types/api'

interface AdminScraperPanelProps {
  onAuthExpired: () => void
}

type FieldDiff = Record<string, { before: string | null; after: string | null }>

function formatDate(iso: string | null | undefined): string {
  if (!iso) return '-'
  return new Date(iso).toLocaleString('pt-PT', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function statusClass(status: string): string {
  switch (status) {
    case 'Succeeded':
      return 'bg-emerald-100 text-emerald-800'
    case 'PartiallySucceeded':
      return 'bg-amber-100 text-amber-800'
    case 'Failed':
      return 'bg-red-100 text-red-700'
    case 'Started':
      return 'bg-blue-100 text-blue-800'
    default:
      return 'bg-gray-100 text-gray-600'
  }
}

function actionClass(action: string): string {
  switch (action) {
    case 'Created':
      return 'bg-emerald-100 text-emerald-800'
    case 'Updated':
      return 'bg-blue-100 text-blue-800'
    case 'Removed':
      return 'bg-red-100 text-red-700'
    default:
      return 'bg-gray-100 text-gray-600'
  }
}

function parseDiff(change: ScrapeListingChange): FieldDiff {
  if (!change.changeSummaryJson) return {}

  try {
    const parsed = JSON.parse(change.changeSummaryJson) as Record<string, { Before?: string | null; After?: string | null; before?: string | null; after?: string | null }>
    return Object.fromEntries(
      Object.entries(parsed).map(([field, value]) => [
        field,
        {
          before: value.before ?? value.Before ?? null,
          after: value.after ?? value.After ?? null,
        },
      ]),
    )
  } catch {
    return {}
  }
}

export default function AdminScraperPanel({ onAuthExpired }: AdminScraperPanelProps) {
  const [sources, setSources] = useState<AdminScraperSource[]>([])
  const [runs, setRuns] = useState<ScraperRunsResponse | null>(null)
  const [selectedRunId, setSelectedRunId] = useState<string | null>(null)
  const [selectedRun, setSelectedRun] = useState<ScraperRunDetail | null>(null)
  const [changes, setChanges] = useState<ScrapeListingChangesResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [runsPage, setRunsPage] = useState(1)
  const [savingSourceId, setSavingSourceId] = useState<string | null>(null)

  const selectedRunSummary = useMemo(
    () => runs?.items.find((run) => run.id === selectedRunId) ?? null,
    [runs, selectedRunId],
  )

  const handleError = useCallback((err: unknown, fallback: string) => {
    if (axios.isAxiosError(err) && err.response?.status === 401) {
      onAuthExpired()
      return
    }
    setError(fallback)
  }, [onAuthExpired])

  const fetchSources = useCallback(async () => {
    const res = await apiClient.get<AdminScraperSource[]>('/admin/scraper-sources')
    setSources(res.data)
  }, [])

  const fetchRuns = useCallback(async () => {
    const res = await apiClient.get<ScraperRunsResponse>(`/admin/scrape-runs?page=${runsPage}&pageSize=20`)
    setRuns(res.data)
    setSelectedRunId((current) => current ?? res.data.items[0]?.id ?? null)
  }, [runsPage])

  const fetchAll = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      await Promise.all([fetchSources(), fetchRuns()])
    } catch (err) {
      handleError(err, 'Erro ao carregar dados dos scrapers.')
    } finally {
      setLoading(false)
    }
  }, [fetchSources, fetchRuns, handleError])

  useEffect(() => {
    fetchAll()
  }, [fetchAll])

  useEffect(() => {
    if (!selectedRunId) {
      setSelectedRun(null)
      setChanges(null)
      return
    }

    let cancelled = false
    async function fetchRunDetails() {
      setError('')
      try {
        const [runRes, changesRes] = await Promise.all([
          apiClient.get<ScraperRunDetail>(`/admin/scrape-runs/${selectedRunId}`),
          apiClient.get<ScrapeListingChangesResponse>(`/admin/scrape-runs/${selectedRunId}/changes?pageSize=100`),
        ])
        if (!cancelled) {
          setSelectedRun(runRes.data)
          setChanges(changesRes.data)
        }
      } catch (err) {
        if (!cancelled) handleError(err, 'Erro ao carregar detalhes da execução.')
      }
    }

    fetchRunDetails()
    return () => {
      cancelled = true
    }
  }, [selectedRunId, handleError])

  async function toggleSource(source: AdminScraperSource) {
    setSavingSourceId(source.id)
    setError('')
    try {
      await apiClient.patch(`/admin/scraper-sources/${source.id}`, { enabled: !source.enabled })
      await fetchSources()
    } catch (err) {
      handleError(err, 'Erro ao actualizar fonte do scraper.')
    } finally {
      setSavingSourceId(null)
    }
  }

  if (loading) {
    return <p className="text-gray-500">A carregar scrapers...</p>
  }

  return (
    <div className="space-y-6">
      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-md">
          {error}
        </div>
      )}

      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold text-gray-900">Fontes de scraping</h2>
        <button
          onClick={fetchAll}
          className="text-sm text-gray-500 hover:text-gray-700 underline"
        >
          Actualizar
        </button>
      </div>

      <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-gray-200">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Fonte</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Scrape</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Ultima execucao</th>
                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Activo</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200 bg-white">
              {sources.map((source) => (
                <tr key={source.id}>
                  <td className="px-4 py-3 align-top">
                    <div className="text-sm font-medium text-gray-900">{source.name}</div>
                    <div className="text-xs text-gray-500">{source.targetDescription ?? source.agencySlug}</div>
                    {source.sourceUrl && (
                      <a
                        href={source.sourceUrl}
                        target="_blank"
                        rel="noreferrer"
                        className="text-xs text-emerald-700 hover:underline break-all"
                      >
                        {source.sourceUrl}
                      </a>
                    )}
                  </td>
                  <td className="px-4 py-3 align-top text-sm text-gray-600">
                    <div>{source.scraperKey}</div>
                    <div className="text-xs text-gray-400">Intervalo {source.interval}</div>
                  </td>
                  <td className="px-4 py-3 align-top">
                    {source.latestRun ? (
                      <div className="space-y-1">
                        <span className={`inline-flex px-2 py-0.5 rounded-full text-xs font-medium ${statusClass(source.latestRun.status)}`}>
                          {source.latestRun.status}
                        </span>
                        <div className="text-xs text-gray-500">{formatDate(source.latestRun.startedAt)}</div>
                        <div className="text-xs text-gray-500">
                          {source.latestRun.listingsCreated} criados, {source.latestRun.listingsUpdated} actualizados, {source.latestRun.listingsRemoved} removidos
                        </div>
                      </div>
                    ) : (
                      <span className="text-sm text-gray-400">Sem execucoes</span>
                    )}
                  </td>
                  <td className="px-4 py-3 align-top text-right">
                    <button
                      onClick={() => toggleSource(source)}
                      disabled={savingSourceId === source.id}
                      className={`inline-flex h-6 w-11 items-center rounded-full transition-colors ${
                        source.enabled ? 'bg-emerald-700' : 'bg-gray-300'
                      } disabled:opacity-60`}
                      aria-label={`${source.enabled ? 'Desactivar' : 'Activar'} ${source.name}`}
                    >
                      <span
                        className={`h-5 w-5 rounded-full bg-white shadow transition-transform ${
                          source.enabled ? 'translate-x-5' : 'translate-x-1'
                        }`}
                      />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-[minmax(0,420px)_1fr] gap-6">
        <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
          <div className="px-4 py-3 border-b border-gray-200">
            <h2 className="text-sm font-semibold text-gray-900">Execucoes</h2>
          </div>
          <div className="divide-y divide-gray-200">
            {runs?.items.length === 0 && (
              <p className="px-4 py-8 text-sm text-gray-500 text-center">Nenhuma execucao registada.</p>
            )}
            {runs?.items.map((run: ScraperRunSummary) => (
              <button
                key={run.id}
                onClick={() => setSelectedRunId(run.id)}
                className={`w-full text-left px-4 py-3 hover:bg-gray-50 ${
                  selectedRunId === run.id ? 'bg-emerald-50' : ''
                }`}
              >
                <div className="flex items-center justify-between gap-3">
                  <span className="text-sm font-medium text-gray-900">{run.sourceName}</span>
                  <span className={`inline-flex px-2 py-0.5 rounded-full text-xs font-medium ${statusClass(run.status)}`}>
                    {run.status}
                  </span>
                </div>
                <div className="text-xs text-gray-500 mt-1">{formatDate(run.startedAt)}</div>
                <div className="text-xs text-gray-500 mt-1">
                  {run.listingsFound} encontrados · {run.listingsCreated} criados · {run.listingsUpdated} actualizados · {run.listingsRemoved} removidos
                </div>
              </button>
            ))}
          </div>
          {runs && runs.totalPages > 1 && (
            <div className="flex items-center justify-between px-4 py-3 border-t border-gray-200 bg-gray-50">
              <span className="text-xs text-gray-500">Pagina {runs.page} de {runs.totalPages}</span>
              <div className="flex gap-2">
                <button
                  disabled={runsPage <= 1}
                  onClick={() => setRunsPage((page) => Math.max(1, page - 1))}
                  className="px-2 py-1 text-xs border border-gray-300 rounded disabled:opacity-50"
                >
                  Anterior
                </button>
                <button
                  disabled={runsPage >= runs.totalPages}
                  onClick={() => setRunsPage((page) => page + 1)}
                  className="px-2 py-1 text-xs border border-gray-300 rounded disabled:opacity-50"
                >
                  Seguinte
                </button>
              </div>
            </div>
          )}
        </div>

        <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
          <div className="px-4 py-3 border-b border-gray-200">
            <h2 className="text-sm font-semibold text-gray-900">
              {selectedRunSummary ? `Alteracoes: ${selectedRunSummary.sourceName}` : 'Alteracoes'}
            </h2>
            {selectedRun && (
              <div className="mt-1 text-xs text-gray-500">
                {selectedRun.sourceTargetDescription ?? selectedRun.sourceUrl ?? 'Sem descricao da fonte'}
              </div>
            )}
          </div>

          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Accao</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Listagem</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Mudancas</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200 bg-white">
                {changes?.items.length === 0 && (
                  <tr>
                    <td colSpan={3} className="px-4 py-10 text-sm text-gray-500 text-center">
                      Nenhuma alteracao registada para esta execucao.
                    </td>
                  </tr>
                )}
                {changes?.items.map((change) => {
                  const diff = parseDiff(change)
                  return (
                    <tr key={change.id}>
                      <td className="px-4 py-3 align-top">
                        <span className={`inline-flex px-2 py-0.5 rounded-full text-xs font-medium ${actionClass(change.action)}`}>
                          {change.action}
                        </span>
                      </td>
                      <td className="px-4 py-3 align-top">
                        <div className="text-sm font-medium text-gray-900 max-w-xs truncate">
                          {change.title ?? change.externalId}
                        </div>
                        <div className="text-xs text-gray-500">{change.externalId}</div>
                        {change.sourceUrl && (
                          <a
                            href={change.sourceUrl}
                            target="_blank"
                            rel="noreferrer"
                            className="text-xs text-emerald-700 hover:underline break-all"
                          >
                            {change.sourceUrl}
                          </a>
                        )}
                      </td>
                      <td className="px-4 py-3 align-top">
                        {Object.keys(diff).length === 0 ? (
                          <span className="text-xs text-gray-400">Sem diferencas de campos principais</span>
                        ) : (
                          <div className="space-y-1">
                            {Object.entries(diff).map(([field, value]) => (
                              <div key={field} className="text-xs text-gray-600">
                                <span className="font-medium text-gray-800">{field}</span>
                                <span className="text-gray-400">: </span>
                                <span>{value.before ?? '-'}</span>
                                <span className="text-gray-400">{' -> '}</span>
                                <span>{value.after ?? '-'}</span>
                              </div>
                            ))}
                          </div>
                        )}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  )
}
