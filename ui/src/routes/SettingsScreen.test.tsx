import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import i18n from '../lib/i18n'
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
  registrationReadiness: {
    ready: true,
    websiteValid: true,
    emailChannelSelected: true,
    emailChannelAvailable: true,
  },
  assets: {},
}

describe('SettingsScreen', () => {
  beforeEach(async () => {
    vi.restoreAllMocks()
    await i18n.changeLanguage('en')
  })

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

  it('shows every registration readiness prerequisite with text status', async () => {
    const unavailable = {
      ...settings,
      contacts: { ...settings.contacts, website: null },
      registrationEnabled: false,
      registrationReadiness: {
        ready: false,
        websiteValid: false,
        emailChannelSelected: true,
        emailChannelAvailable: true,
      },
    }
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(unavailable))
    renderScreen()

    await screen.findByDisplayValue('The finest shard')
    const readiness = screen.getByRole('region', { name: /registration readiness/i })
    expect(within(readiness).getByText('Overall')).toBeInTheDocument()
    expect(within(readiness).getByText('Not ready')).toBeInTheDocument()
    expect(within(readiness).getByText('Website')).toBeInTheDocument()
    expect(within(readiness).getByText('Invalid')).toBeInTheDocument()
    expect(within(readiness).getByText('Verification channel')).toBeInTheDocument()
    expect(within(readiness).getByText('Selected')).toBeInTheDocument()
    expect(within(readiness).getByText('Email channel')).toBeInTheDocument()
    expect(within(readiness).getByText('Available')).toBeInTheDocument()
  })

  it.each(['', 'http:/portal', 'https:/portal', 'http:portal', 'http:///portal', 'ftp://shard.example'])(
    'prevents enabling registration with invalid draft Website %j',
    async (website) => {
      const unavailable = {
        ...settings,
        contacts: { ...settings.contacts, website },
        registrationEnabled: false,
        registrationReadiness: {
          ready: false,
          websiteValid: false,
          emailChannelSelected: true,
          emailChannelAvailable: true,
        },
      }
      vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(unavailable))
      renderScreen()

      await screen.findByDisplayValue('The finest shard')
      expect(screen.getByRole('switch', { name: /registration/i })).toBeDisabled()
    },
  )

  it('immediately permits enabling registration when the draft Website becomes valid', async () => {
    const unavailable = {
      ...settings,
      contacts: { ...settings.contacts, website: 'http:/portal' },
      registrationEnabled: false,
      registrationReadiness: {
        ready: false,
        websiteValid: false,
        emailChannelSelected: true,
        emailChannelAvailable: true,
      },
    }
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(unavailable))
    renderScreen()

    await screen.findByDisplayValue('http:/portal')
    const toggle = screen.getByRole('switch', { name: /registration/i })
    expect(toggle).toBeDisabled()

    const website = screen.getByLabelText(/^website$/i)
    await userEvent.clear(website)
    await userEvent.type(website, 'https://shard.example/moongate')

    expect(toggle).toBeEnabled()
    const readiness = screen.getByRole('region', { name: /registration readiness/i })
    expect(within(readiness).getByText('Ready')).toBeInTheDocument()
    expect(within(readiness).getByText('Valid')).toBeInTheDocument()
  })

  it('PUTs a valid draft Website and registration enablement atomically', async () => {
    const unavailable = {
      ...settings,
      contacts: { ...settings.contacts, website: null },
      registrationEnabled: false,
      registrationReadiness: {
        ready: false,
        websiteValid: false,
        emailChannelSelected: true,
        emailChannelAvailable: true,
      },
    }
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(unavailable))
    renderScreen()

    await screen.findByDisplayValue('The finest shard')
    const website = screen.getByLabelText(/^website$/i)
    await userEvent.type(website, 'https://shard.example/moongate')
    await userEvent.click(screen.getByRole('switch', { name: /registration/i }))
    await userEvent.click(screen.getByRole('button', { name: /save/i }))

    await waitFor(() => {
      const puts = fetchSpy.mock.calls.filter(([, init]) => (init as RequestInit)?.method === 'PUT')
      expect(puts).toHaveLength(1)
      expect(JSON.parse((puts[0][1] as RequestInit).body as string)).toMatchObject({
        contacts: { website: 'https://shard.example/moongate' },
        registrationEnabled: true,
      })
    })
  })

  it('allows unhealthy persisted registration to be switched off', async () => {
    const unhealthy = {
      ...settings,
      contacts: { ...settings.contacts, website: null },
      registrationReadiness: {
        ready: false,
        websiteValid: false,
        emailChannelSelected: false,
        emailChannelAvailable: false,
      },
    }
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(unhealthy))
    renderScreen()

    const toggle = screen.getByRole('switch', { name: /registration/i })
    await waitFor(() => expect(toggle).toBeChecked())
    expect(toggle).toBeEnabled()
    await userEvent.click(toggle)
    await userEvent.click(screen.getByRole('button', { name: /save/i }))

    await waitFor(() => {
      const put = fetchSpy.mock.calls.find(([, init]) => (init as RequestInit)?.method === 'PUT')!
      expect(JSON.parse((put[1] as RequestInit).body as string)).toMatchObject({
        registrationEnabled: false,
      })
    })
  })

  it('guides channel configuration without exposing SMTP credentials', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(settings))
    renderScreen()

    await screen.findByDisplayValue('The finest shard')
    const readiness = screen.getByRole('region', { name: /registration readiness/i })
    expect(within(readiness).getByText(/moongate\.yaml/i)).toBeInTheDocument()
    expect(within(readiness).getByText(/plugins\/configs\/smtp\.yaml|environment variables/i)).toBeInTheDocument()
    expect(within(readiness).getByText(/restart the server/i)).toBeInTheDocument()
    expect(within(readiness).queryByText(/host|username|password|sender/i)).not.toBeInTheDocument()
  })
})
