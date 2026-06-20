import { describe, it, expect, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import FilterSidebar from '@/components/FilterSidebar'
import { useFilterStore } from '@/stores/filterStore'

// Reset the store before each test
beforeEach(() => {
  useFilterStore.setState({
    priceMin: undefined,
    priceMax: undefined,
    type: undefined,
    bedrooms: undefined,
    transaction: 'sale',
    mobileOpen: false,
  })
})

describe('FilterSidebar', () => {
  it('renders all filter sections', () => {
    render(<FilterSidebar />)

    // Use getAllByText for texts that appear in both desktop + mobile sheet
    expect(screen.getAllByText('Filtros').length).toBeGreaterThanOrEqual(1)
    expect(screen.getAllByText('Tipo de imóvel').length).toBeGreaterThanOrEqual(1)
    expect(screen.getAllByText('Faixa de preço (€)').length).toBeGreaterThanOrEqual(1)
    expect(screen.getAllByText('Quartos').length).toBeGreaterThanOrEqual(1)
  })

  it('updates property type on button click', async () => {
    const user = userEvent.setup()
    render(<FilterSidebar />)

    const moradiaButtons = screen.getAllByText('Moradia')
    await user.click(moradiaButtons[0])

    expect(useFilterStore.getState().type).toBe('house')
  })

  it('updates price range inputs', async () => {
    const user = userEvent.setup()
    render(<FilterSidebar />)

    const minInputs = screen.getAllByPlaceholderText('Mín.')
    const maxInputs = screen.getAllByPlaceholderText('Máx.')
    const minInput = minInputs[0]
    const maxInput = maxInputs[0]

    await user.type(minInput, '100000')
    await user.type(maxInput, '200000')

    expect(useFilterStore.getState().priceMin).toBe(100000)
    expect(useFilterStore.getState().priceMax).toBe(200000)
  })

  it('shows clear filters button when filters are active', async () => {
    const user = userEvent.setup()
    render(<FilterSidebar />)

    // No clear button initially
    expect(screen.queryAllByText('Limpar filtros').length).toBe(0)

    // Activate a filter
    const moradiaButtons = screen.getAllByText('Moradia')
    await user.click(moradiaButtons[0])

    // Clear button appears (may appear in desktop + mobile versions)
    expect(screen.getAllByText('Limpar filtros').length).toBeGreaterThanOrEqual(1)
  })

  it('clears all filters when clear button is clicked', async () => {
    const user = userEvent.setup()
    render(<FilterSidebar />)

    // Set some filters
    const moradiaButtons = screen.getAllByText('Moradia')
    await user.click(moradiaButtons[0])
    expect(useFilterStore.getState().transaction).toBe('sale')
    expect(useFilterStore.getState().type).toBe('house')

    // Click clear
    const clearButtons = screen.getAllByText('Limpar filtros')
    await user.click(clearButtons[0])
    expect(useFilterStore.getState().transaction).toBe('sale')
    expect(useFilterStore.getState().type).toBeUndefined()
  })

  it('updates bedrooms via select dropdown', async () => {
    const user = userEvent.setup()
    render(<FilterSidebar />)

    const bedroomSelect = screen.getAllByLabelText('Quartos')[0]
    await user.selectOptions(bedroomSelect, '3')

    expect(useFilterStore.getState().bedrooms).toBe(3)
  })

  it('resets bedrooms to undefined when selecting "Qualquer"', async () => {
    const user = userEvent.setup()
    render(<FilterSidebar />)

    const bedroomSelect = screen.getAllByLabelText('Quartos')[0]
    await user.selectOptions(bedroomSelect, '3')
    expect(useFilterStore.getState().bedrooms).toBe(3)

    await user.selectOptions(bedroomSelect, '')
    expect(useFilterStore.getState().bedrooms).toBeUndefined()
  })

  it('renders all property type buttons', () => {
    render(<FilterSidebar />)

    expect(screen.getAllByText('Moradia').length).toBeGreaterThanOrEqual(1)
    expect(screen.getAllByText('Apartamento').length).toBeGreaterThanOrEqual(1)
    expect(screen.getAllByText('Terreno').length).toBeGreaterThanOrEqual(1)
    expect(screen.getAllByText('Comercial').length).toBeGreaterThanOrEqual(1)
    expect(screen.getAllByText('Outro').length).toBeGreaterThanOrEqual(1)
  })

})
