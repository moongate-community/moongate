import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import '../lib/i18n'
import { SettingsScreen } from './SettingsScreen'

function renderScreen() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <SettingsScreen />
    </QueryClientProvider>,
  )
}

function json(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } })
}

const settings = {
  description: 'The finest shard',
  tagline: 'Sosaria never sleeps.',
  contacts: { website: 'https://x.io', email: null, discord: null },
  registrationEnabled: true,
  assets: {},
}

describe('SettingsScreen', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('shows the fetched settings', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(settings))
    renderScreen()

    expect(await screen.findByDisplayValue('The finest shard')).toBeInTheDocument()
    expect(screen.getByDisplayValue('Sosaria never sleeps.')).toBeInTheDocument()
    expect(screen.getByDisplayValue('https://x.io')).toBeInTheDocument()
  })

  it('renders an upload row for every asset slot', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(settings))
    renderScreen()

    await screen.findByDisplayValue('The finest shard')
    expect(screen.getByTestId('asset-input-logo')).toBeInTheDocument()
    expect(screen.getByTestId('asset-input-favicon')).toBeInTheDocument()
    expect(screen.getByTestId('asset-input-banner')).toBeInTheDocument()
  })

  it('matches a stored asset to its slot regardless of key casing', async () => {
    // The API keys the assets map in PascalCase ("Logo"); the slot is lowercase.
    const withLogo = { ...settings, assets: { Logo: '/api/v1/server-info/assets/logo' } }
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(withLogo))
    renderScreen()

    expect(await screen.findByRole('img', { name: /^logo$/i })).toBeInTheDocument()
  })

  it('PUTs the current form state on Save', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(settings))
    renderScreen()

    // The registration toggle is seeded on (from the fetched settings); flip it off and save.
    const toggle = await screen.findByRole('switch', { name: /registration/i })
    await userEvent.click(toggle)
    await userEvent.click(screen.getByRole('button', { name: /save/i }))

    await waitFor(() => {
      const put = fetchSpy.mock.calls.find(([, init]) => (init as RequestInit)?.method === 'PUT')!
      expect(JSON.parse((put[1] as RequestInit).body as string)).toMatchObject({
        registrationEnabled: false,
        description: 'The finest shard',
        tagline: 'Sosaria never sleeps.',
      })
    })
  })
})
