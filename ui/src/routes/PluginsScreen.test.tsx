import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
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

  const plugins = [
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
    {
      id: 'moongate.news',
      name: 'News',
      version: '0.4.0',
      author: 'moongate',
      description: '',
      assembly: 'Moongate.News.Plugin',
      isExternal: false,
      routes: [],
    },
  ]

  it('lists the active plugins in a table', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(plugins))
    renderScreen()

    expect(await screen.findByText('HTTP')).toBeInTheDocument()
    expect(screen.getByText('News')).toBeInTheDocument()
    expect(screen.getAllByText('0.4.0')).toHaveLength(2)
  })

  it('filters the table by search', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(plugins))
    renderScreen()
    await screen.findByText('HTTP')

    await userEvent.type(screen.getByPlaceholderText(/search plugins/i), 'news')
    expect(screen.queryByText('HTTP')).not.toBeInTheDocument()
    expect(screen.getByText('News')).toBeInTheDocument()
  })
})
