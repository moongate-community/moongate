import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import '../lib/i18n'
import { PluginsScreen } from './PluginsScreen'

function renderScreen() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <PluginsScreen />
    </QueryClientProvider>,
  )
}

function json(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } })
}

describe('PluginsScreen', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('lists the active plugins with their route counts', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      json([
        {
          id: 'moongate.http',
          name: 'HTTP',
          version: '0.4.0',
          author: 'moongate',
          description: '',
          assembly: 'Moongate.Http.Plugin',
          isExternal: true,
          routes: [{ method: 'GET', path: '/x', policy: null }],
        },
      ]),
    )
    renderScreen()

    expect(await screen.findByText('HTTP')).toBeInTheDocument()
    expect(screen.getByText('0.4.0')).toBeInTheDocument()
  })
})
