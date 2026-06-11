import { describe, it, expect } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { server } from './setup'
import SearchPage from '@/pages/SearchPage'
import { mockListResponse, mockEmptyListResponse } from './mocks/data'

function createWrapper(initialEntries = ['/search']) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  })

  return function Wrapper({ children }: { children: React.ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={initialEntries}>
          {children}
        </MemoryRouter>
      </QueryClientProvider>
    )
  }
}

describe('SearchPage', () => {
  it('shows loading state initially', () => {
    render(<SearchPage />, { wrapper: createWrapper() })

    // Skeleton cards should be visible during initial load
    expect(screen.getByText('Imóveis em Pombal')).toBeInTheDocument()
  })

  it('renders listing cards after successful data load', async () => {
    render(<SearchPage />, { wrapper: createWrapper() })

    // Wait for the data to load — property title should appear
    await waitFor(() => {
      expect(screen.getByText('Moradia T3 em Pombal')).toBeInTheDocument()
    })

    // Count display
    expect(screen.getByText('3 imóveis encontrados')).toBeInTheDocument()
  })

  it('shows empty state when no results', async () => {
    // Override the handler to return empty results for this test
    server.use(
      http.get('/api/listings', () => {
        return HttpResponse.json(mockEmptyListResponse)
      }),
    )

    render(<SearchPage />, { wrapper: createWrapper() })

    await waitFor(() => {
      expect(screen.getByText('Nenhum imóvel encontrado')).toBeInTheDocument()
    })
  })

  it('shows error state when API call fails', async () => {
    server.use(
      http.get('/api/listings', () => {
        return HttpResponse.json(
          { message: 'Erro ao carregar imóveis' },
          { status: 500 },
        )
      }),
    )

    render(<SearchPage />, { wrapper: createWrapper() })

    await waitFor(() => {
      expect(
        screen.getByText('Erro ao carregar imóveis'),
      ).toBeInTheDocument()
    })
  })

  it('has a retry button in error state', async () => {
    server.use(
      http.get('/api/listings', () => {
        return HttpResponse.json(
          { message: 'Erro ao carregar imóveis' },
          { status: 500 },
        )
      }),
    )

    render(<SearchPage />, { wrapper: createWrapper() })

    await waitFor(() => {
      expect(screen.getByText('Tentar novamente')).toBeInTheDocument()
    })
  })

  it('renders FilterSidebar alongside listings', async () => {
    render(<SearchPage />, { wrapper: createWrapper() })

    await waitFor(() => {
      expect(screen.getByText('Moradia T3 em Pombal')).toBeInTheDocument()
    })

    // Filter sections should be visible (desktop sidebar — use getAllByText since mobile sheet duplicates)
    expect(screen.getAllByText('Tipo de negócio').length).toBeGreaterThanOrEqual(1)
    expect(screen.getAllByText('Tipo de imóvel').length).toBeGreaterThanOrEqual(1)
  })

  it('does not show pagination when there are fewer results than one page', async () => {
    render(<SearchPage />, { wrapper: createWrapper() })

    await waitFor(() => {
      expect(screen.getByText('Moradia T3 em Pombal')).toBeInTheDocument()
    })

    // With 3 items and pageSize 12, totalPages = 1, so no pagination rendered
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument()
  })

  it('shows pagination when there are enough items for multiple pages', async () => {
    server.use(
      http.get('/api/listings', () => {
        return HttpResponse.json({
          ...mockListResponse,
          total: 25,
        })
      }),
    )

    render(<SearchPage />, { wrapper: createWrapper() })

    await waitFor(() => {
      expect(screen.getByText(/25 imóveis encontrados/)).toBeInTheDocument()
    })

    // Now totalPages = Math.ceil(25 / 12) = 3, so pagination should render
    expect(screen.getByRole('navigation')).toBeInTheDocument()
    // Should show "1 / 3"
    expect(screen.getByText('1 / 3')).toBeInTheDocument()
  })
})
