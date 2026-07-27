import { render, screen, waitFor, act, fireEvent } from '@testing-library/react'
import { AuthProvider, useSession } from './auth'

function Probe() {
  const session = useSession()
  return (
    <div data-testid="state">
      {session.status}:{session.username ?? '-'}
    </div>
  )
}

function SignInProbe() {
  const session = useSession()
  return (
    <>
      <div data-testid="state">
        {session.status}:{session.username ?? '-'}
      </div>
      <button onClick={() => void session.signIn('tom', 'secret')}>sign in</button>
    </>
  )
}

function mockJson(status: number, body: unknown) {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } })
}

describe('AuthProvider', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.restoreAllMocks()
    vi.useRealTimers()
  })

  it('is anonymous with no stored token', async () => {
    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    )
    await waitFor(() => expect(screen.getByTestId('state')).toHaveTextContent('anonymous:-'))
  })

  it('validates a stored token at boot', async () => {
    localStorage.setItem('mg-token', JSON.stringify({ token: 't', expiresAt: futureIso() }))
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJson(200, { username: 'tom', level: 'Administrator' }))

    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    )

    await waitFor(() => expect(screen.getByTestId('state')).toHaveTextContent('authenticated:tom'))
  })

  it('drops a stored token the server rejects', async () => {
    localStorage.setItem('mg-token', JSON.stringify({ token: 'stale', expiresAt: futureIso() }))
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJson(401, {}))

    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    )

    await waitFor(() => expect(screen.getByTestId('state')).toHaveTextContent('anonymous:-'))
    expect(localStorage.getItem('mg-token')).toBeNull()
  })

  it('adopts the issued token before asking who it belongs to', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input)
      if (url.endsWith('/api/v1/auth/login')) {
        return mockJson(200, { token: 'issued', expiresAt: futureIso() })
      }
      return mockJson(200, { username: 'tom', level: 'Player' })
    })

    render(
      <AuthProvider>
        <SignInProbe />
      </AuthProvider>,
    )
    await waitFor(() => expect(screen.getByTestId('state')).toHaveTextContent('anonymous:-'))

    await act(async () => {
      fireEvent.click(screen.getByRole('button'))
    })

    await waitFor(() => expect(screen.getByTestId('state')).toHaveTextContent('authenticated:tom'))
    expect(JSON.parse(localStorage.getItem('mg-token')!).token).toBe('issued')

    // The order is the point: asking who the token belongs to before adopting it would send the request
    // with no Authorization header, and a login that had just succeeded would answer 401.
    const me = fetchSpy.mock.calls.find(([input]) => String(input).endsWith('/api/v1/player/me'))!
    expect(new Headers((me[1] as RequestInit).headers).get('authorization')).toBe('Bearer issued')
  })

  it('renews before the token expires', async () => {
    vi.useFakeTimers()
    localStorage.setItem('mg-token', JSON.stringify({ token: 'first', expiresAt: inSeconds(300) }))

    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input)
      if (url.endsWith('/api/v1/auth/renew')) {
        return mockJson(200, { token: 'second', expiresAt: inSeconds(3600) })
      }
      return mockJson(200, { username: 'tom', level: 'Player' })
    })

    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    )
    await vi.waitFor(() => expect(screen.getByTestId('state')).toHaveTextContent('authenticated:tom'))

    // Renewal is scheduled 120s before expiry, i.e. after 180s here.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(181_000)
    })

    expect(fetchSpy.mock.calls.some(([input]) => String(input).endsWith('/api/v1/auth/renew'))).toBe(true)
    await vi.waitFor(() => expect(JSON.parse(localStorage.getItem('mg-token')!).token).toBe('second'))
  })

  it('signs out when renewal is refused', async () => {
    vi.useFakeTimers()
    localStorage.setItem('mg-token', JSON.stringify({ token: 'first', expiresAt: inSeconds(300) }))

    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input)
      if (url.endsWith('/api/v1/auth/renew')) {
        return mockJson(401, {})
      }
      return mockJson(200, { username: 'tom', level: 'Player' })
    })

    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    )
    await vi.waitFor(() => expect(screen.getByTestId('state')).toHaveTextContent('authenticated:tom'))

    await act(async () => {
      await vi.advanceTimersByTimeAsync(181_000)
    })

    await vi.waitFor(() => expect(screen.getByTestId('state')).toHaveTextContent('anonymous:-'))
    expect(localStorage.getItem('mg-token')).toBeNull()
  })
})

function futureIso() {
  return inSeconds(3600)
}

function inSeconds(s: number) {
  return new Date(Date.now() + s * 1000).toISOString()
}
