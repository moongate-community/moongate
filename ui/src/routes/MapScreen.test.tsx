import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import '../lib/i18n'
import { MapScreen } from './MapScreen'
import type { LiveMapProps } from '../components/map/LiveMap'

const authState = vi.hoisted(() => ({
  level: 'Administrator' as string | null,
}))

vi.mock('../lib/auth', () => ({
  useSession: () => ({ level: authState.level }),
}))

// Leaflet can't lay out in jsdom, so the map is replaced by a stub that records the props it was given as
// data-attributes and exposes a button to fire onHover — enough to drive every control.
vi.mock('../components/map/LiveMap', () => ({
  LiveMap: (props: LiveMapProps) => (
    <div
      data-testid="live-map"
      data-facet={props.facet.name}
      data-style={props.style}
      data-center={props.centerTarget ? `${props.centerTarget.x},${props.centerTarget.y}` : ''}
      data-players={props.players.map((player) => player.characterName).join(',')}
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

const onlinePlayers = [
  {
    characterSerial: '0x42',
    characterName: 'Freydis',
    accountSerial: '0x01',
    accountUsername: 'alice',
    mapId: 0,
    mapName: 'Felucca',
    x: 1420,
    y: 1698,
    z: 10,
    direction: 'SouthEast',
    running: false,
    body: 401,
    skinHue: 1002,
    hits: 48,
    hitsMax: 70,
    warmode: false,
  },
  {
    characterSerial: '0x43',
    characterName: 'Dupre',
    accountSerial: '0x02',
    accountUsername: 'bob',
    mapId: 2,
    mapName: 'Ilshenar',
    x: 100,
    y: 200,
    z: 0,
    direction: 'North',
    running: true,
    body: 400,
    skinHue: 0,
    hits: 60,
    hitsMax: 60,
    warmode: false,
  },
  {
    characterSerial: '0x44',
    characterName: 'Lost',
    accountSerial: '0x03',
    accountUsername: 'carol',
    mapId: 99,
    mapName: 'Unknown',
    x: 10,
    y: 10,
    z: 0,
    direction: 'West',
    running: false,
    body: 400,
    skinHue: 0,
    hits: 10,
    hitsMax: 10,
    warmode: false,
  },
]

function json(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } })
}

function renderScreen() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const view = render(
    <QueryClientProvider client={client}>
      <MapScreen />
    </QueryClientProvider>,
  )

  return { ...view, client }
}

describe('MapScreen', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    authState.level = 'Administrator'
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input)
      if (url.includes('/api/v1/images/maps')) return json(facets)
      if (url.includes('/api/v1/admin/players/online')) return json(onlinePlayers)
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
    const writeText = vi.fn().mockResolvedValue(undefined)
    Object.assign(navigator, { clipboard: { writeText } })
    renderScreen()
    await screen.findByTestId('live-map')
    await userEvent.click(screen.getByRole('button', { name: 'hover' }))
    await userEvent.click(await screen.findByText('1234, 5678'))
    expect(writeText).toHaveBeenCalledWith('1234, 5678')
  })

  it('resets the jump target when the facet changes', async () => {
    renderScreen()
    await screen.findByTestId('live-map')
    await userEvent.type(screen.getByLabelText('X'), '1000')
    await userEvent.type(screen.getByLabelText('Y'), '2000')
    await userEvent.click(screen.getByRole('button', { name: 'Go' }))
    await waitFor(() => expect(screen.getByTestId('live-map')).toHaveAttribute('data-center', '1000,2000'))

    await userEvent.selectOptions(screen.getByLabelText('Facet'), 'Ilshenar')
    await waitFor(() => expect(screen.getByTestId('live-map')).toHaveAttribute('data-center', ''))
  })

  it.each(['Administrator', 'GrandMaster'])('shows the online-player overlay to %s', async (level) => {
    authState.level = level
    renderScreen()

    expect(await screen.findByLabelText('Online players')).toBeChecked()
    expect(await screen.findByText('1 / 3')).toBeInTheDocument()
    expect(screen.getByTestId('live-map')).toHaveAttribute('data-players', 'Freydis')
  })

  it('never exposes or requests the admin overlay for a player account', async () => {
    authState.level = 'Player'
    const fetchSpy = vi.mocked(globalThis.fetch)
    renderScreen()

    await screen.findByTestId('live-map')
    expect(screen.queryByLabelText('Online players')).not.toBeInTheDocument()
    expect(screen.getByTestId('live-map')).toHaveAttribute('data-players', '')
    expect(fetchSpy.mock.calls.some(([input]) => String(input).includes('/api/v1/admin/players/online'))).toBe(false)
  })

  it('switches the visible player subset with the facet', async () => {
    renderScreen()
    expect(await screen.findByText('1 / 3')).toBeInTheDocument()
    expect(screen.getByTestId('live-map')).toHaveAttribute('data-players', 'Freydis')

    await userEvent.selectOptions(screen.getByLabelText('Facet'), 'Ilshenar')

    await waitFor(() => expect(screen.getByTestId('live-map')).toHaveAttribute('data-players', 'Dupre'))
    expect(screen.getByText('1 / 3')).toBeInTheDocument()
  })

  it('hides players, disables polling, and refetches when re-enabled', async () => {
    const fetchSpy = vi.mocked(globalThis.fetch)
    renderScreen()

    const toggle = await screen.findByLabelText('Online players')
    await screen.findByText('1 / 3')
    const initialRequests = fetchSpy.mock.calls.filter(([input]) =>
      String(input).includes('/api/v1/admin/players/online'),
    ).length

    await userEvent.click(toggle)
    expect(toggle).not.toBeChecked()
    expect(screen.getByTestId('live-map')).toHaveAttribute('data-players', '')
    expect(screen.getByText('0 / 3')).toBeInTheDocument()

    await userEvent.click(toggle)
    expect(toggle).toBeChecked()
    await waitFor(() =>
      expect(
        fetchSpy.mock.calls.filter(([input]) => String(input).includes('/api/v1/admin/players/online')).length,
      ).toBeGreaterThan(initialRequests),
    )
  })

  it('keeps the map usable when the player snapshot fails', async () => {
    vi.mocked(globalThis.fetch).mockImplementation(async (input) => {
      const url = String(input)
      if (url.includes('/api/v1/images/maps')) return json(facets)
      if (url.includes('/api/v1/admin/players/online')) {
        return new Response(null, { status: 503 })
      }
      return json({})
    })

    renderScreen()

    expect(await screen.findByTestId('live-map')).toBeInTheDocument()
    expect(await screen.findByText('Player positions are temporarily unavailable.')).toBeInTheDocument()
    expect(screen.getByTestId('live-map')).toHaveAttribute('data-players', '')
  })

  it('retains the last marker snapshot when a background refetch fails', async () => {
    let playerRequestFails = false
    vi.mocked(globalThis.fetch).mockImplementation(async (input) => {
      const url = String(input)
      if (url.includes('/api/v1/images/maps')) return json(facets)
      if (url.includes('/api/v1/admin/players/online')) {
        return playerRequestFails ? new Response(null, { status: 503 }) : json(onlinePlayers)
      }
      return json({})
    })

    const { client } = renderScreen()
    expect(await screen.findByText('1 / 3')).toBeInTheDocument()
    expect(screen.getByTestId('live-map')).toHaveAttribute('data-players', 'Freydis')

    playerRequestFails = true
    await client.invalidateQueries({ queryKey: ['admin', 'players', 'online'] })

    expect(await screen.findByText('Player positions are temporarily unavailable.')).toBeInTheDocument()
    expect(screen.getByTestId('live-map')).toHaveAttribute('data-players', 'Freydis')
    expect(screen.getByText('1 / 3')).toBeInTheDocument()
  })
})
