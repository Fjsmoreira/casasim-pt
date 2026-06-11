import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ListingCard from '@/components/listing/ListingCard'
import { mockProperty, mockPropertyNoImage, mockPropertyRent } from './mocks/data'

describe('ListingCard', () => {
  it('renders price, type, area and bedrooms for a sale property', () => {
    render(<ListingCard property={mockProperty} />)

    // Price formatted with pt-PT locale — may use . or , as separator
    expect(screen.getByText(/€\d/)).toBeInTheDocument()

    // Title
    expect(screen.getByText('Moradia T3 em Pombal')).toBeInTheDocument()

    // Area badge
    expect(screen.getByText('150m²')).toBeInTheDocument()

    // Bedrooms badge (text is split across elements due to icon)
    expect(screen.getByText(/3\s*quartos/)).toBeInTheDocument()

    // Property type badge
    expect(screen.getByText('Casa')).toBeInTheDocument()

    // Transaction badge
    expect(screen.getByText('Venda')).toBeInTheDocument()

    // Location
    expect(screen.getByText('Pombal, Pombal')).toBeInTheDocument()
  })

  it('renders rent badge with /mês suffix', () => {
    render(<ListingCard property={mockPropertyRent} />)

    expect(screen.getByText(/\/mês/)).toBeInTheDocument()
    expect(screen.getByText('Arrendamento')).toBeInTheDocument()
  })

  it('does not render area or bedrooms when not provided', () => {
    const propWithoutExtras = {
      ...mockProperty,
      areaM2: undefined,
      bedrooms: undefined,
    }
    render(<ListingCard property={propWithoutExtras} />)

    expect(screen.queryByText(/m²/)).not.toBeInTheDocument()
    expect(screen.queryByText(/quarto/)).not.toBeInTheDocument()
  })

  it('renders placeholder when no image is available', () => {
    render(<ListingCard property={mockPropertyNoImage} />)

    // The placeholder shows a Home icon in a green gradient div
    // The img tag should not be present
    expect(screen.queryByRole('img')).not.toBeInTheDocument()
  })

  it('renders an image when images are provided', () => {
    render(<ListingCard property={mockProperty} />)

    const img = screen.getByRole('img')
    expect(img).toBeInTheDocument()
    expect(img).toHaveAttribute('src', 'https://example.com/photo.jpg')
  })

  it('calls onFavoriteToggle when favorite button is clicked', async () => {
    const onFavoriteToggle = vi.fn()
    const user = userEvent.setup()

    render(<ListingCard property={mockProperty} onFavoriteToggle={onFavoriteToggle} />)

    const favButton = screen.getByLabelText('Adicionar aos favoritos')
    await user.click(favButton)

    expect(onFavoriteToggle).toHaveBeenCalledWith('prop-1')
  })

  it('toggles favorite aria-label on click', async () => {
    const user = userEvent.setup()

    render(<ListingCard property={mockProperty} />)

    const favButton = screen.getByLabelText('Adicionar aos favoritos')
    await user.click(favButton)

    expect(screen.getByLabelText('Remover dos favoritos')).toBeInTheDocument()
  })

  it('renders location defaulting to Pombal when no city/parish', () => {
    const propNoLocation = {
      ...mockProperty,
      city: undefined,
      parish: undefined,
    }
    render(<ListingCard property={propNoLocation} />)

    expect(screen.getByText('Pombal')).toBeInTheDocument()
  })

  it('renders bedroom singular for 1 bedroom', () => {
    const singleBed = { ...mockProperty, bedrooms: 1 }
    render(<ListingCard property={singleBed} />)

    expect(screen.getByText('1 quarto')).toBeInTheDocument()
  })

  it('renders unknown type label gracefully', () => {
    const unknownType = { ...mockProperty, type: 'other' as const }
    render(<ListingCard property={unknownType} />)

    expect(screen.getByText('Outro')).toBeInTheDocument()
  })
})
