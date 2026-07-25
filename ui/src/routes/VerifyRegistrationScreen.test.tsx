import { StrictMode, useEffect } from 'react'
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import i18n from '../lib/i18n'
import { AuthProvider } from '../lib/auth'
import { VerifyRegistrationScreen } from './VerifyRegistrationScreen'

type VerifyResult = { status: number; problem?: unknown } | { status: 'network' } | { status: 'pending' }

type ObservedLocation = {
  pathname: string
  search: string
  state: unknown
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  })
}

function servePublicApi(verifyResults: VerifyResult[] = [{ status: 200 }]) {
  let verifyAttempt = 0

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

    if (url.endsWith('/api/v1/register/verify')) {
      const result = verifyResults[Math.min(verifyAttempt, verifyResults.length - 1)]
      verifyAttempt += 1

      if (result.status === 'network') {
        throw new TypeError('offline')
      }

      if (result.status === 'pending') {
        return new Promise<Response>(() => {})
      }

      return result.status === 200
        ? new Response(null, { status: 200 })
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

function LocationProbe({ onChange }: { onChange: (location: ObservedLocation) => void }) {
  const location = useLocation()

  useEffect(() => {
    onChange({
      pathname: location.pathname,
      search: location.search,
      state: location.state,
    })
  }, [location.pathname, location.search, location.state, onChange])

  return null
}

function renderVerification(initialEntry = '/verify?token=secret-bearer-token') {
  const locations: ObservedLocation[] = []
  const observeLocation = (location: ObservedLocation) => locations.push(location)

  const view = render(
    <StrictMode>
      <MemoryRouter initialEntries={[initialEntry]}>
        <QueryClientProvider client={createTestClient()}>
          <AuthProvider>
            <LocationProbe onChange={observeLocation} />
            <Routes>
              <Route path="/verify" element={<VerifyRegistrationScreen />} />
            </Routes>
          </AuthProvider>
        </QueryClientProvider>
      </MemoryRouter>
    </StrictMode>,
  )

  return { locations, view }
}

function verifyCalls(fetchSpy: ReturnType<typeof servePublicApi>) {
  return fetchSpy.mock.calls.filter(([input]) => String(input).endsWith('/api/v1/register/verify'))
}

describe('VerifyRegistrationScreen', () => {
  beforeEach(async () => {
    localStorage.clear()
    sessionStorage.clear()
    vi.restoreAllMocks()
    await i18n.changeLanguage('en')
  })

  afterEach(() => {
    cleanup()
  })

  it('scrubs the bearer token, verifies once under Strict Mode, and leaves browser storage untouched', async () => {
    const token = 'strict-mode-secret-token'
    localStorage.setItem('sentinel', 'local-value')
    sessionStorage.setItem('sentinel', 'session-value')
    const fetchSpy = servePublicApi()
    const { locations } = renderVerification(`/verify?token=${token}`)

    expect(await screen.findByRole('heading', { name: /email verified/i })).toBeVisible()
    expect(screen.getByRole('link', { name: /sign in/i })).toHaveAttribute('href', '/login')

    const calls = verifyCalls(fetchSpy)
    expect(calls).toHaveLength(1)
    expect(calls[0][1]).toMatchObject({ method: 'POST' })
    expect(JSON.parse(String((calls[0][1] as RequestInit).body))).toEqual({ token })

    const finalLocation = locations[locations.length - 1]
    expect(finalLocation).toEqual({ pathname: '/verify', search: '', state: null })
    expect(document.documentElement.outerHTML).not.toContain(token)
    expect(localStorage.getItem('sentinel')).toBe('local-value')
    expect(sessionStorage.getItem('sentinel')).toBe('session-value')
    expect(localStorage.length).toBe(1)
    expect(sessionStorage.length).toBe(1)
  })

  it('announces verification progress politely while the request is pending', async () => {
    servePublicApi([{ status: 'pending' }])
    renderVerification()

    const status = await screen.findByRole('status')
    expect(status).toHaveAttribute('aria-live', 'polite')
    expect(status).toHaveAttribute('aria-atomic', 'true')
    expect(screen.getByRole('heading', { name: /verifying your account/i })).toBeVisible()
  })

  it('shows an expired-link state with a path to request another email for 410', async () => {
    servePublicApi([{ status: 410 }])
    renderVerification()

    expect(await screen.findByRole('heading', { name: /verification link expired/i })).toBeVisible()
    expect(screen.getByRole('link', { name: /request another email/i })).toHaveAttribute('href', '/register/pending')
    expect(screen.queryByText(/sensitive backend detail/i)).not.toBeInTheDocument()
  })

  it('shows safe invalid-link actions for 400', async () => {
    servePublicApi([{ status: 400 }])
    renderVerification()

    expect(await screen.findByRole('heading', { name: /verification link invalid/i })).toBeVisible()
    expect(screen.getByRole('link', { name: /request another email/i })).toHaveAttribute('href', '/register/pending')
    expect(screen.getByRole('link', { name: /sign in/i })).toHaveAttribute('href', '/login')
    expect(screen.queryByText(/sensitive backend detail/i)).not.toBeInTheDocument()
  })

  it('treats a missing token as invalid without making a verification request', async () => {
    const fetchSpy = servePublicApi()
    const { locations } = renderVerification('/verify')

    expect(await screen.findByRole('heading', { name: /verification link invalid/i })).toBeVisible()
    expect(verifyCalls(fetchSpy)).toHaveLength(0)
    expect(locations[locations.length - 1]).toEqual({ pathname: '/verify', search: '', state: null })
  })

  it.each([
    ['network failure', { status: 'network' }],
    ['503 response', { status: 503 }],
  ] as const)('retries a %s with only the in-memory token', async (_scenario, failure) => {
    const token = 'retry-only-memory-token'
    const fetchSpy = servePublicApi([failure, { status: 200 }])
    renderVerification(`/verify?token=${token}`)

    expect(await screen.findByRole('heading', { name: /verification temporarily unavailable/i })).toBeVisible()
    await userEvent.click(screen.getByRole('button', { name: /try again/i }))

    expect(await screen.findByRole('heading', { name: /email verified/i })).toBeVisible()
    const calls = verifyCalls(fetchSpy)
    expect(calls).toHaveLength(2)
    expect(calls.map(([, init]) => JSON.parse(String((init as RequestInit).body)))).toEqual([{ token }, { token }])
    expect(document.documentElement.outerHTML).not.toContain(token)
    expect(localStorage.length).toBe(0)
    expect(sessionStorage.length).toBe(0)
  })
})
