import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import i18n from '../lib/i18n'
import { AuthProvider } from '../lib/auth'
import { RegisterScreen } from './RegisterScreen'

type RegisterResult =
  | { status: number; problem?: unknown }
  | {
      status: 'network'
    }

type PublicApiOptions = {
  registrationEnabled?: boolean
  serverInfoAvailable?: boolean
  registerResults?: RegisterResult[]
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  })
}

function servePublicApi({
  registrationEnabled = true,
  serverInfoAvailable = true,
  registerResults = [{ status: 202 }],
}: PublicApiOptions = {}) {
  let registerAttempt = 0

  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const url = String(input)

    if (url.endsWith('/api/v1/server-info')) {
      if (!serverInfoAvailable) {
        throw new TypeError('offline')
      }

      return json({
        shardName: 'Moongate',
        tagline: null,
        contacts: { website: null, email: null, discord: null },
        registrationEnabled,
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

    if (url.endsWith('/api/v1/register')) {
      const result = registerResults[Math.min(registerAttempt, registerResults.length - 1)]
      registerAttempt += 1

      if (result.status === 'network') {
        throw new TypeError('offline')
      }

      return result.status === 202
        ? new Response(null, { status: 202 })
        : json(result.problem ?? { title: 'request failed' }, result.status)
    }

    return json({})
  })
}

function PendingProbe() {
  const location = useLocation()
  return <pre>{JSON.stringify(location.state)}</pre>
}

function createTestClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
}

function renderRegister(client = createTestClient()) {
  return render(
    <MemoryRouter initialEntries={['/register']}>
      <QueryClientProvider client={client}>
        <AuthProvider>
          <Routes>
            <Route path="/register" element={<RegisterScreen />} />
            <Route path="/register/pending" element={<PendingProbe />} />
          </Routes>
        </AuthProvider>
      </QueryClientProvider>
    </MemoryRouter>,
  )
}

async function fillRegistration({
  username = 'player.one',
  email = 'player@example.test',
  password = 'correct horse',
  confirmation = password,
}: {
  username?: string
  email?: string
  password?: string
  confirmation?: string
} = {}) {
  await userEvent.type(await screen.findByLabelText(/account name/i), username)
  await userEvent.type(screen.getByLabelText(/^email$/i), email)
  await userEvent.type(screen.getByLabelText(/^password$/i), password)
  await userEvent.type(screen.getByLabelText(/confirm password/i), confirmation)
}

function registerCalls(fetchSpy: ReturnType<typeof servePublicApi>) {
  return fetchSpy.mock.calls.filter(([input]) => String(input).endsWith('/api/v1/register'))
}

describe('RegisterScreen', () => {
  beforeEach(async () => {
    localStorage.clear()
    sessionStorage.clear()
    vi.restoreAllMocks()
    await i18n.changeLanguage('en')
  })

  it.each([
    ['disabled', { registrationEnabled: false }],
    ['unreachable', { serverInfoAvailable: false }],
  ])('shows an informational state and login link when registration is %s', async (_, options) => {
    servePublicApi(options)
    renderRegister()

    expect(await screen.findByRole('heading', { name: /registration is unavailable/i })).toBeInTheDocument()
    expect(screen.getByText(/not accepting public registrations/i)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /back to sign in/i })).toHaveAttribute('href', '/login')
    expect(screen.queryByRole('form')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /create account/i })).not.toBeInTheDocument()
  })

  it('hides a cached enabled form when the current server-info refetch fails', async () => {
    const client = createTestClient()
    client.setQueryData(['server-info'], {
      shardName: 'Moongate',
      tagline: null,
      contacts: { website: null, email: null, discord: null },
      registrationEnabled: true,
      assets: {},
    })
    const fetchSpy = servePublicApi({ serverInfoAvailable: false })

    renderRegister(client)

    await waitFor(() =>
      expect(fetchSpy.mock.calls.some(([input]) => String(input).endsWith('/api/v1/server-info'))).toBe(true),
    )
    expect(await screen.findByRole('heading', { name: /registration is unavailable/i })).toBeInTheDocument()
    expect(screen.queryByRole('form', { name: /create account/i })).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: /back to sign in/i })).toHaveAttribute('href', '/login')
  })

  it('shows visible requirements and the exact registration autocomplete values', async () => {
    servePublicApi()
    renderRegister()

    const username = await screen.findByLabelText(/account name/i)
    const email = screen.getByLabelText(/^email$/i)
    const password = screen.getByLabelText(/^password$/i)
    const confirmation = screen.getByLabelText(/confirm password/i)

    expect(username).toHaveAttribute('autocomplete', 'username')
    expect(email).toHaveAttribute('autocomplete', 'email')
    expect(password).toHaveAttribute('autocomplete', 'new-password')
    expect(confirmation).toHaveAttribute('autocomplete', 'new-password')
    expect(screen.getByText(/3–30 letters/i)).toBeVisible()
    expect(screen.getByText(/8–30 printable characters/i)).toBeVisible()
  })

  it('submits the exact normalized payload and navigates with identity state only', async () => {
    const fetchSpy = servePublicApi()
    renderRegister()

    await fillRegistration({
      username: '  player.one  ',
      email: '  player@example.test  ',
    })
    await userEvent.click(screen.getByRole('button', { name: /create account/i }))

    const state = await screen.findByText('{"username":"player.one","email":"player@example.test"}', undefined, {
      timeout: 2000,
    })
    const calls = registerCalls(fetchSpy)
    expect(calls).toHaveLength(1)
    expect(JSON.parse(String((calls[0][1] as RequestInit).body))).toEqual({
      username: 'player.one',
      email: 'player@example.test',
      password: 'correct horse',
    })
    expect(state).not.toHaveTextContent(/password|correct horse/i)
    expect(localStorage.length).toBe(0)
    expect(sessionStorage.length).toBe(0)
  })

  it('attaches every client validation error to its field without submitting', async () => {
    const fetchSpy = servePublicApi()
    renderRegister()

    await fillRegistration({
      username: 'ab',
      email: 'invalid',
      password: 'short',
      confirmation: 'different',
    })
    await userEvent.click(screen.getByRole('button', { name: /create account/i }))

    const expectedErrors = [
      ['account name', /valid account name/i],
      ['email', /valid email address/i],
      ['password', /valid password/i],
      ['confirm password', /passwords do not match/i],
    ] as const

    for (const [label, message] of expectedErrors) {
      const input = screen.getByLabelText(new RegExp(`^${label}$`, 'i'))
      const errorId = input.getAttribute('aria-describedby')
      expect(input).toHaveAttribute('aria-invalid', 'true')
      expect(errorId).toBeTruthy()
      const alert = document.getElementById(errorId ?? '')
      expect(alert).toHaveAttribute('role', 'alert')
      expect(alert).toHaveTextContent(message)
    }

    expect(registerCalls(fetchSpy)).toHaveLength(0)
  })

  it('maps backend validation keys to localized field errors without trusting backend copy', async () => {
    servePublicApi({
      registerResults: [
        {
          status: 400,
          problem: {
            detail: 'sensitive backend detail',
            errors: {
              username: ['backend username copy'],
              email: ['backend email copy'],
              password: ['backend password copy'],
            },
          },
        },
      ],
    })
    renderRegister()
    await fillRegistration()
    await userEvent.click(screen.getByRole('button', { name: /create account/i }))

    const form = await screen.findByRole('form', { name: /create account/i })
    expect(within(form).getByText(/valid account name/i)).toHaveAttribute('role', 'alert')
    expect(within(form).getByText(/valid email address/i)).toHaveAttribute('role', 'alert')
    expect(within(form).getByText(/valid password/i)).toHaveAttribute('role', 'alert')
    expect(screen.queryByText(/backend .* copy|sensitive backend detail/i)).not.toBeInTheDocument()
  })

  it.each([
    [409, /username or email is unavailable/i],
    [429, /too many attempts/i],
    [403, /no longer available/i],
    [503, /verification email is temporarily unavailable/i],
  ] as const)('maps registration status %s to a polite localized form error', async (status, message) => {
    servePublicApi({ registerResults: [{ status }] })
    renderRegister()
    await fillRegistration()
    await userEvent.click(screen.getByRole('button', { name: /create account/i }))

    const error = await screen.findByText(message)
    expect(error).toHaveAttribute('aria-live', 'polite')
    expect(screen.getAllByText(message)).toHaveLength(1)
  })

  it('maps a network error to generic localized copy and retries with every typed value retained', async () => {
    const fetchSpy = servePublicApi({
      registerResults: [{ status: 'network' }, { status: 202 }],
    })
    renderRegister()
    await fillRegistration()
    await userEvent.click(screen.getByRole('button', { name: /create account/i }))

    const formError = await screen.findByText(/something went wrong/i)
    expect(formError).toHaveAttribute('aria-live', 'polite')
    expect(screen.getByLabelText(/account name/i)).toHaveValue('player.one')
    expect(screen.getByLabelText(/^email$/i)).toHaveValue('player@example.test')
    expect(screen.getByLabelText(/^password$/i)).toHaveValue('correct horse')
    expect(screen.getByLabelText(/confirm password/i)).toHaveValue('correct horse')

    await userEvent.click(screen.getByRole('button', { name: /create account/i }))

    await waitFor(() => expect(registerCalls(fetchSpy)).toHaveLength(2))
    for (const call of registerCalls(fetchSpy)) {
      expect(JSON.parse(String((call[1] as RequestInit).body))).toEqual({
        username: 'player.one',
        email: 'player@example.test',
        password: 'correct horse',
      })
    }
    expect(await screen.findByText('{"username":"player.one","email":"player@example.test"}')).toBeInTheDocument()
  })
})
