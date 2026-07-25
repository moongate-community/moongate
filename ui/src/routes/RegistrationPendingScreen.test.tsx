import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, MemoryRouter, Route, Routes } from 'react-router'
import i18n from '../lib/i18n'
import { AuthProvider } from '../lib/auth'
import { RegistrationPendingScreen } from './RegistrationPendingScreen'

type ResendResult =
  | { status: number; problem?: unknown }
  | {
      status: 'network'
    }

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  })
}

function servePublicApi(resendResults: ResendResult[] = [{ status: 202 }]) {
  let resendAttempt = 0

  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const url = String(input)

    if (url.endsWith('/api/v1/server-info')) {
      return json({
        shardName: 'Moongate',
        tagline: null,
        contacts: { website: null, email: null, discord: null },
        registrationEnabled: true,
        assets: {},
      })
    }

    if (url.endsWith('/api/v1/stats')) {
      return json({
        players: { online: 0, connections: 0 },
        accounts: { total: 0, active: 0, characters: 0 },
      })
    }

    if (url.endsWith('/api/v1/version')) {
      return json({ shardName: 'Moongate', version: '1.0.0' })
    }

    if (url.endsWith('/api/v1/register/resend')) {
      const result = resendResults[Math.min(resendAttempt, resendResults.length - 1)]
      resendAttempt += 1

      if (result.status === 'network') {
        throw new TypeError('offline')
      }

      return result.status === 202
        ? new Response(null, { status: 202 })
        : json(result.problem ?? { detail: 'sensitive backend detail' }, result.status)
    }

    return json({})
  })
}

function createTestClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
}

function renderPending(state?: unknown) {
  return render(
    <MemoryRouter initialEntries={[{ pathname: '/register/pending', state }]}>
      <QueryClientProvider client={createTestClient()}>
        <AuthProvider>
          <Routes>
            <Route path="/register/pending" element={<RegistrationPendingScreen />} />
          </Routes>
        </AuthProvider>
      </QueryClientProvider>
    </MemoryRouter>,
  )
}

function renderBrowserPending() {
  return render(
    <BrowserRouter>
      <QueryClientProvider client={createTestClient()}>
        <AuthProvider>
          <Routes>
            <Route path="/register/pending" element={<RegistrationPendingScreen />} />
          </Routes>
        </AuthProvider>
      </QueryClientProvider>
    </BrowserRouter>,
  )
}

function resendCalls(fetchSpy: ReturnType<typeof servePublicApi>) {
  return fetchSpy.mock.calls.filter(([input]) => String(input).endsWith('/api/v1/register/resend'))
}

describe('RegistrationPendingScreen', () => {
  beforeEach(async () => {
    localStorage.clear()
    sessionStorage.clear()
    window.history.replaceState(null, '', '/')
    vi.restoreAllMocks()
    await i18n.changeLanguage('en')
  })

  afterEach(() => {
    cleanup()
  })

  it('prefills valid transient registration identity state without persisting it', async () => {
    servePublicApi()
    renderPending({ username: 'player.one', email: 'player@example.test' })

    expect(await screen.findByLabelText(/account name/i)).toHaveValue('player.one')
    expect(screen.getByLabelText(/^email$/i)).toHaveValue('player@example.test')
    expect(screen.getByText(/look for a verification email/i)).toBeVisible()
    expect(localStorage.length).toBe(0)
    expect(sessionStorage.length).toBe(0)
  })

  it('uses navigation state once and clears it before a subsequent browser reload', async () => {
    servePublicApi()
    window.history.replaceState(
      { usr: { username: 'player.one', email: 'player@example.test' }, key: 'pending', idx: 0 },
      '',
      '/register/pending',
    )

    const firstMount = renderBrowserPending()

    expect(await screen.findByLabelText(/account name/i)).toHaveValue('player.one')
    expect(screen.getByLabelText(/^email$/i)).toHaveValue('player@example.test')

    firstMount.unmount()
    renderBrowserPending()

    expect(await screen.findByLabelText(/account name/i)).toHaveValue('')
    expect(screen.getByLabelText(/^email$/i)).toHaveValue('')
  })

  it.each([undefined, { username: 123, email: null }])(
    'uses blank fields for direct or malformed state',
    async (state) => {
      servePublicApi()
      renderPending(state)

      expect(await screen.findByLabelText(/account name/i)).toHaveValue('')
      expect(screen.getByLabelText(/^email$/i)).toHaveValue('')
    },
  )

  it('posts the normalized valid resend identity', async () => {
    const fetchSpy = servePublicApi()
    renderPending()

    await userEvent.type(await screen.findByLabelText(/account name/i), '  player.one  ')
    await userEvent.type(screen.getByLabelText(/^email$/i), '  player@example.test  ')
    await userEvent.click(screen.getByRole('button', { name: /resend verification email/i }))

    await screen.findByText(/your request has been received/i)
    const calls = resendCalls(fetchSpy)
    expect(calls).toHaveLength(1)
    expect(JSON.parse(String((calls[0][1] as RequestInit).body))).toEqual({
      username: 'player.one',
      email: 'player@example.test',
    })
  })

  it('shows the identical generic confirmation for distinct accepted identities', async () => {
    const confirmations: string[] = []

    for (const state of [
      { username: 'player.one', email: 'player@example.test' },
      { username: 'someone.else', email: 'other@example.test' },
    ]) {
      servePublicApi([{ status: 202 }])
      const view = renderPending(state)

      await userEvent.click(await screen.findByRole('button', { name: /resend verification email/i }))
      confirmations.push((await screen.findByText(/your request has been received/i)).textContent ?? '')

      view.unmount()
      vi.restoreAllMocks()
    }

    expect(confirmations).toEqual([
      'Your request has been received. Check your inbox for a verification email.',
      'Your request has been received. Check your inbox for a verification email.',
    ])
  })

  it('shows local field errors without requesting resend for invalid details', async () => {
    const fetchSpy = servePublicApi()
    renderPending()

    await userEvent.type(await screen.findByLabelText(/account name/i), 'ab')
    await userEvent.type(screen.getByLabelText(/^email$/i), 'invalid')
    await userEvent.click(screen.getByRole('button', { name: /resend verification email/i }))

    expect(await screen.findByText(/enter a valid account name/i)).toHaveAttribute('role', 'alert')
    expect(screen.getByText(/enter a valid email address/i)).toHaveAttribute('role', 'alert')
    expect(resendCalls(fetchSpy)).toHaveLength(0)
  })

  it.each([
    [400, /check the account name and email and try again/i],
    [429, /too many resend attempts/i],
    [503, /verification email is temporarily unavailable/i],
    ['network', /something went wrong/i],
  ] as const)('maps resend failure %s to safe localized copy', async (result, message) => {
    servePublicApi([{ status: result }])
    renderPending({ username: 'player.one', email: 'player@example.test' })

    await userEvent.click(await screen.findByRole('button', { name: /resend verification email/i }))

    expect(await screen.findByText(message)).toHaveAttribute('aria-live', 'polite')
    expect(screen.queryByText(/sensitive backend detail/i)).not.toBeInTheDocument()
  })

  it('disables only resend submission while the request is pending', async () => {
    let resolveRequest: ((value: Response) => void) | undefined
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      if (String(input).endsWith('/api/v1/register/resend')) {
        return new Promise<Response>((resolve) => {
          resolveRequest = resolve
        })
      }

      if (String(input).endsWith('/api/v1/server-info')) {
        return json({
          shardName: 'Moongate',
          tagline: null,
          contacts: { website: null, email: null, discord: null },
          registrationEnabled: true,
          assets: {},
        })
      }

      if (String(input).endsWith('/api/v1/stats')) {
        return json({
          players: { online: 0, connections: 0 },
          accounts: { total: 0, active: 0, characters: 0 },
        })
      }

      if (String(input).endsWith('/api/v1/version')) {
        return json({ shardName: 'Moongate', version: '1.0.0' })
      }

      return json({})
    })
    renderPending({ username: 'player.one', email: 'player@example.test' })

    const username = await screen.findByLabelText(/account name/i)
    const email = screen.getByLabelText(/^email$/i)
    const submit = screen.getByRole('button', { name: /resend verification email/i })
    await userEvent.click(submit)

    await waitFor(() => expect(submit).toBeDisabled())
    expect(username).not.toBeDisabled()
    expect(email).not.toBeDisabled()
    expect(screen.getByRole('link', { name: /back to sign in/i })).not.toHaveAttribute('aria-disabled', 'true')

    resolveRequest?.(new Response(null, { status: 202 }))
    await screen.findByText(/your request has been received/i)
  })

  it('does not write resend details to browser storage', async () => {
    servePublicApi()
    renderPending({ username: 'player.one', email: 'player@example.test' })

    await userEvent.click(await screen.findByRole('button', { name: /resend verification email/i }))
    await screen.findByText(/your request has been received/i)

    expect(localStorage.length).toBe(0)
    expect(sessionStorage.length).toBe(0)
  })
})
