import type { ReactElement } from 'react'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import '../lib/i18n'
import { AppShell } from './AppShell'
import { AuthProvider } from '../lib/auth'

// The bar reads the shard's name and its live player count, so it needs a query client. Retries are
// off: a test that mocks a failing fetch should see the failure now, not after three attempts.
function wrap(ui: ReactElement) {
  return (
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <MemoryRouter>
        <AuthProvider>{ui}</AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>
  )
}

function renderShell() {
  return render(
    wrap(
      <AppShell>
        <p>content</p>
      </AppShell>,
    ),
  )
}

function json(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } })
}

/**
 * A fresh Response per call, routed by URL. One shared Response object cannot serve the bar's three
 * requests: its body is read once and every later read throws "Body has already been read".
 */
function stubFetch(level: string) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation((input) => {
    const url = String(input)

    if (url.includes('/version')) {
      return Promise.resolve(json({ shardName: 'Moongate', version: '0.4.2' }))
    }

    if (url.includes('/stats')) {
      return Promise.resolve(json({ players: { online: 3, connections: 3 } }))
    }

    return Promise.resolve(json({ username: 'tom', level }))
  })
}

function renderWithLevel(level: string) {
  localStorage.setItem(
    'mg-token',
    JSON.stringify({ token: 't', expiresAt: new Date(Date.now() + 3.6e6).toISOString() }),
  )
  stubFetch(level)

  return render(
    wrap(
      <AppShell>
        <p>content</p>
      </AppShell>,
    ),
  )
}

describe('AppShell', () => {
  beforeEach(() => {
    localStorage.clear()
    sessionStorage.clear()
    vi.restoreAllMocks()
  })

  it('shows the brand, the tab row and the routed content', () => {
    renderShell()

    expect(screen.getByText('Moongate')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /dashboard/i })).toBeInTheDocument()
    expect(screen.getByText('content')).toBeInTheDocument()
  })

  it('offers the theme toggle', () => {
    renderShell()
    // Dark is the default, so the toggle offers the switch to light.
    expect(screen.getByRole('button', { name: /light/i })).toBeInTheDocument()
  })

  // Characters is a player's own page, so unlike Admin right beside it, it is not gated on level.
  it('shows the Characters tab to a player', async () => {
    renderWithLevel('Player')
    expect(await screen.findByRole('link', { name: /characters/i })).toBeInTheDocument()
  })

  it('shows the Admin tab for an admin session', async () => {
    renderWithLevel('Administrator')
    expect(await screen.findByRole('link', { name: /admin/i })).toBeInTheDocument()
  })

  it('hides the Admin tab from a player', async () => {
    renderWithLevel('Player')
    await waitFor(() => expect(screen.getByText('tom')).toBeInTheDocument())
    expect(screen.queryByRole('link', { name: /admin/i })).not.toBeInTheDocument()
  })

  // The bar states what the shard is and how it is doing. The design draws a shard picker here; there
  // is one shard, so this names it rather than pretending you could switch.
  it('names the shard and its build', async () => {
    renderWithLevel('Administrator')

    expect(await screen.findByText('Moongate')).toBeInTheDocument()
    expect(await screen.findByText('v0.4.2')).toBeInTheDocument()
  })

  // Green because the shard answered, not because a badge looks nice: if /stats fails there is no
  // LIVE, which is the only thing that makes it worth showing.
  it('shows LIVE only while the shard is answering', async () => {
    renderWithLevel('Administrator')
    expect(await screen.findByText('LIVE')).toBeInTheDocument()

    cleanup()
    localStorage.clear()
    vi.spyOn(globalThis, 'fetch').mockRejectedValue(new Error('down'))

    render(
      wrap(
        <AppShell>
          <p>content</p>
        </AppShell>,
      ),
    )

    await waitFor(() => expect(screen.queryByText('LIVE')).not.toBeInTheDocument())
  })

  it('reports how many players are in the world', async () => {
    renderWithLevel('Administrator')

    expect(await screen.findByText('3 online')).toBeInTheDocument()
  })

  // Staff are marked as staff: the design gives them the purple badge, players just their name.
  it('badges a staff session with its level', async () => {
    renderWithLevel('Administrator')

    expect(await screen.findByText('Administrator · tom')).toBeInTheDocument()
  })

  it('shows a player their name without a badge', async () => {
    renderWithLevel('Player')

    expect(await screen.findByText('tom')).toBeInTheDocument()
    expect(screen.queryByText('Player · tom')).not.toBeInTheDocument()
  })
})
