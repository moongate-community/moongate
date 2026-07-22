import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import '../lib/i18n'
import { DashboardScreen } from './DashboardScreen'

function renderDashboard() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <DashboardScreen />
    </QueryClientProvider>,
  )
}

function json(body: unknown) {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'content-type': 'application/json' },
  })
}

describe('DashboardScreen', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('lists the account characters and the shard figures', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input)

      if (url.endsWith('/api/v1/player/me')) return json({ username: 'tom', level: 'Administrator' })
      if (url.endsWith('/api/v1/player/me/characters')) {
        return json([{ serial: '0x40000001', name: 'Aelric', race: 'Human' }])
      }
      if (url.endsWith('/api/v1/stats')) {
        return json({
          players: { online: 42, connections: 45 },
          accounts: { total: 7, active: 5, characters: 19 },
        })
      }
      return json({})
    })

    renderDashboard()

    expect(await screen.findByText('Aelric')).toBeInTheDocument()
    expect(await screen.findByText('42')).toBeInTheDocument()
  })

  it('shows an empty state when the account has no characters', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input)

      if (url.endsWith('/api/v1/player/me/characters')) return json([])
      if (url.endsWith('/api/v1/stats')) {
        return json({
          players: { online: 0, connections: 0 },
          accounts: { total: 1, active: 1, characters: 0 },
        })
      }
      return json({ username: 'tom', level: 'Player' })
    })

    renderDashboard()

    expect(await screen.findByText(/nessun personaggio/i)).toBeInTheDocument()
  })
})
