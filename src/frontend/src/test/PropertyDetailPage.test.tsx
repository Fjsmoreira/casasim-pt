import { describe, it, expect } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import PropertyDetailPage from '@/pages/PropertyDetailPage'

function createWrapper(propertyId: string) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  })

  return function Wrapper() {
    return (
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={[`/listings/${propertyId}`]}>
          <Routes>
            <Route path="/listings/:id" element={<PropertyDetailPage />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    )
  }
}

describe('PropertyDetailPage', () => {
  it('shows loading skeleton while fetching', () => {
    render(<PropertyDetailPage />, {
      wrapper: createWrapper('prop-1'),
    })

    // Loading skeleton should be visible
    expect(
      screen.getByText('A carregar detalhes do imóvel...'),
    ).toBeInTheDocument()
  })

  it('renders property details after successful load', async () => {
    render(<PropertyDetailPage />, {
      wrapper: createWrapper('prop-1'),
    })

    // Wait for the property title to appear
    await waitFor(() => {
      expect(screen.getByText('Moradia T3 em Pombal')).toBeInTheDocument()
    })

    // Key details should render
    expect(screen.getByText('Voltar para resultados')).toBeInTheDocument()
  })

  it('shows 404 page when property is not found', async () => {
    render(<PropertyDetailPage />, {
      wrapper: createWrapper('404-test'),
    })

    await waitFor(() => {
      expect(
        screen.getByText('Imóvel não encontrado'),
      ).toBeInTheDocument()
    })

    // Should have a link back to search
    expect(screen.getByText('Ver imóveis disponíveis')).toBeInTheDocument()
  })

  it('shows generic error page on server error', async () => {
    render(<PropertyDetailPage />, {
      wrapper: createWrapper('error-test'),
    })

    await waitFor(() => {
      expect(
        screen.getByText('Erro ao carregar o imóvel'),
      ).toBeInTheDocument()
    })

    // Should have a retry link
    expect(screen.getByText('Tentar novamente')).toBeInTheDocument()
  })
})
