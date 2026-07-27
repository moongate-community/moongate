import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import '../lib/i18n'
import { AdminScreen } from './AdminScreen'

function renderAdmin() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <AdminScreen />
    </QueryClientProvider>,
  )
}

function json(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } })
}

describe('AdminScreen', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('shows shard status and statistics', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input)

      if (url.endsWith('/api/v1/admin/status')) {
        return json({ shardName: 'Moongate', version: '1.2.3', onlineSessions: 4 })
      }
      if (url.endsWith('/api/v1/stats')) {
        return json({
          players: { online: 42, connections: 45 },
          accounts: { total: 7, active: 5, characters: 19 },
          world: { npcs: 12, items: 340 },
          content: { itemTemplates: 1665, mobileTemplates: 19 },
        })
      }
      return json({})
    })

    renderAdmin()

    expect(await screen.findByText('1.2.3')).toBeInTheDocument() // build
    expect(await screen.findByText('4')).toBeInTheDocument() // sessions
    expect(await screen.findByText('42')).toBeInTheDocument() // players online
  })
})
