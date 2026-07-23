import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import '../lib/i18n'
import { AuthProvider } from '../lib/auth'
import { LoginScreen } from './LoginScreen'

function renderLogin() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })

  return render(
    <MemoryRouter>
      <QueryClientProvider client={client}>
        <AuthProvider>
          <LoginScreen />
        </AuthProvider>
      </QueryClientProvider>
    </MemoryRouter>,
  )
}

describe('LoginScreen', () => {
  beforeEach(() => {
    localStorage.clear()
    sessionStorage.clear()
    vi.restoreAllMocks()
    assets = {}
    tagline = null
  })

  function json(body: unknown, status = 200) {
    return new Response(JSON.stringify(body), {
      status,
      headers: { 'content-type': 'application/json' },
    })
  }

  /**
   * Answers each route with its real shape. A catch-all that returned the account body for everything
   * would hand /api/v1/stats a payload with no `players`, and the screen would crash on a field the
   * contract declares required — a failure invented by the mock, not by the code.
   */
  function serveApi() {
    return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input)

      if (url.endsWith('/api/v1/auth/login')) {
        return json({ token: 't', expiresAt: new Date(Date.now() + 3.6e6).toISOString() })
      }
      if (url.endsWith('/api/v1/stats')) {
        return json({
          players: { online: 184, connections: 190 },
          accounts: { total: 7, active: 5, characters: 19 },
        })
      }
      if (url.endsWith('/api/v1/version')) {
        return json({ shardName: 'Moongate', version: '9.9.9' })
      }
      if (url.endsWith('/api/v1/server-info')) {
        return json({
          shardName: 'Moongate',
          tagline,
          contacts: { website: null, email: null, discord: null },
          registrationEnabled: false,
          assets,
        })
      }
      return json({ username: 'tom', level: 'Player' })
    })
  }

  // What the /server-info mock reports; tests override these before rendering.
  let assets: Record<string, string> = {}
  let tagline: string | null = null

  it('shows the server version once it resolves', async () => {
    serveApi()
    renderLogin()

    expect(await screen.findByText(/Moongate · v9\.9\.9/)).toBeInTheDocument()
  })

  it('shows the shard logo when the server publishes one', async () => {
    assets = { Logo: '/api/v1/server-info/assets/logo' }
    serveApi()
    renderLogin()

    const logo = await screen.findByRole('img', { name: /shard logo/i })
    expect(logo).toHaveAttribute('src', expect.stringContaining('/api/v1/server-info/assets/logo'))
  })

  it('shows no shard logo when the server has none', async () => {
    serveApi()
    renderLogin()

    // The version resolving proves server-info also had time to; the logo is genuinely absent.
    await screen.findByText(/Moongate · v9\.9\.9/)
    expect(screen.queryByRole('img', { name: /shard logo/i })).not.toBeInTheDocument()
  })

  it('shows the shard tagline when the server sets one', async () => {
    tagline = 'The moongates hum tonight.'
    serveApi()
    renderLogin()

    expect(await screen.findByText(/The moongates hum tonight\./)).toBeInTheDocument()
  })

  it('falls back to the default quote without a tagline', async () => {
    serveApi()
    renderLogin()

    expect(await screen.findByText(/Sosaria never sleeps\./)).toBeInTheDocument()
  })

  it('sends the credentials and stores the issued token', async () => {
    serveApi()

    renderLogin()

    await userEvent.type(screen.getByLabelText(/account name/i), 'tom')
    await userEvent.type(screen.getByLabelText(/password/i), 'secret')
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }))

    await waitFor(() => expect(JSON.parse(localStorage.getItem('mg-token')!).token).toBe('t'))
  })

  // The checkbox has to move the token, not just look checkable: unticked, the session must not outlive
  // the tab, which is the whole promise "remember me on this device" makes by being optional.
  it('keeps an unremembered session out of localStorage', async () => {
    serveApi()

    renderLogin()

    await userEvent.type(screen.getByLabelText(/account name/i), 'tom')
    await userEvent.type(screen.getByLabelText(/password/i), 'secret')
    await userEvent.click(screen.getByLabelText(/remember me/i))
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }))

    await waitFor(() => expect(JSON.parse(sessionStorage.getItem('mg-token')!).token).toBe('t'))
    expect(localStorage.getItem('mg-token')).toBeNull()
  })

  it('shows a message when the credentials are refused', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(json({}, 401))

    renderLogin()

    await userEvent.type(screen.getByLabelText(/account name/i), 'tom')
    await userEvent.type(screen.getByLabelText(/password/i), 'wrong')
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }))

    expect(await screen.findByText(/invalid/i)).toBeInTheDocument()
  })
})
