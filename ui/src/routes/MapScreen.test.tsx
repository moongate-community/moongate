import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import '../lib/i18n'
import { MapScreen } from './MapScreen'
import type { LiveMapProps } from '../components/map/LiveMap'

// Leaflet can't lay out in jsdom, so the map is replaced by a stub that records the props it was given as
// data-attributes and exposes a button to fire onHover — enough to drive every control.
vi.mock('../components/map/LiveMap', () => ({
  LiveMap: (props: LiveMapProps) => (
    <div
      data-testid="live-map"
      data-facet={props.facet.name}
      data-style={props.style}
      data-center={props.centerTarget ? `${props.centerTarget.x},${props.centerTarget.y}` : ''}
    >
      <button type="button" onClick={() => props.onHover({ x: 1234, y: 5678 })}>
        hover
      </button>
    </div>
  ),
}))

const facets = [
  { name: 'Felucca', width: 6144, height: 4096, maxZoom: 5, tileSize: 256, tilesAcross: 24, tilesDown: 16 },
  { name: 'Ilshenar', width: 2304, height: 1600, maxZoom: 4, tileSize: 256, tilesAcross: 9, tilesDown: 7 },
]

function json(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } })
}

function renderScreen() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MapScreen />
    </QueryClientProvider>,
  )
}

describe('MapScreen', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      if (String(input).includes('/api/v1/images/maps')) return json(facets)
      return json({})
    })
  })

  it('defaults to Felucca and renders the map', async () => {
    renderScreen()
    expect(await screen.findByTestId('live-map')).toHaveAttribute('data-facet', 'Felucca')
  })

  it('switches facet', async () => {
    renderScreen()
    await screen.findByTestId('live-map')
    await userEvent.selectOptions(screen.getByLabelText('Facet'), 'Ilshenar')
    await waitFor(() => expect(screen.getByTestId('live-map')).toHaveAttribute('data-facet', 'Ilshenar'))
  })

  it('toggles the relief style', async () => {
    renderScreen()
    await screen.findByTestId('live-map')
    await userEvent.click(screen.getByLabelText('Relief'))
    await waitFor(() => expect(screen.getByTestId('live-map')).toHaveAttribute('data-style', 'relief'))
  })

  it('centers on a valid jump coordinate', async () => {
    renderScreen()
    await screen.findByTestId('live-map')
    await userEvent.type(screen.getByLabelText('X'), '1000')
    await userEvent.type(screen.getByLabelText('Y'), '2000')
    await userEvent.click(screen.getByRole('button', { name: 'Go' }))
    await waitFor(() => expect(screen.getByTestId('live-map')).toHaveAttribute('data-center', '1000,2000'))
  })

  it('rejects an out-of-bounds jump without moving the map', async () => {
    renderScreen()
    await screen.findByTestId('live-map')
    await userEvent.type(screen.getByLabelText('X'), '99999')
    await userEvent.type(screen.getByLabelText('Y'), '10')
    await userEvent.click(screen.getByRole('button', { name: 'Go' }))
    expect(await screen.findByRole('alert')).toBeInTheDocument()
    expect(screen.getByTestId('live-map')).toHaveAttribute('data-center', '')
  })

  it('shows the hovered coordinate and copies it on click', async () => {
    const writeText = vi.fn()
    Object.assign(navigator, { clipboard: { writeText } })
    renderScreen()
    await screen.findByTestId('live-map')
    await userEvent.click(screen.getByRole('button', { name: 'hover' }))
    await userEvent.click(await screen.findByText('1234, 5678'))
    expect(writeText).toHaveBeenCalledWith('1234, 5678')
  })
})
