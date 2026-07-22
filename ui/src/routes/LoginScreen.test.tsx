import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import '../lib/i18n'
import { AuthProvider } from '../lib/auth'
import { LoginScreen } from './LoginScreen'

function renderLogin() {
  return render(
    <MemoryRouter>
      <AuthProvider>
        <LoginScreen />
      </AuthProvider>
    </MemoryRouter>,
  )
}

describe('LoginScreen', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.restoreAllMocks()
  })

  it('sends the credentials and stores the issued token', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) =>
      String(input).endsWith('/api/v1/auth/login')
        ? new Response(
            JSON.stringify({ token: 't', expiresAt: new Date(Date.now() + 3.6e6).toISOString() }),
            { status: 200, headers: { 'content-type': 'application/json' } },
          )
        : new Response(JSON.stringify({ username: 'tom', level: 'Player' }), {
            status: 200,
            headers: { 'content-type': 'application/json' },
          }),
    )

    renderLogin()

    await userEvent.type(screen.getByLabelText(/account name/i), 'tom')
    await userEvent.type(screen.getByLabelText(/password/i), 'secret')
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }))

    await waitFor(() => expect(JSON.parse(localStorage.getItem('mg-token')!).token).toBe('t'))
  })

  it('shows a message when the credentials are refused', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response('{}', { status: 401, headers: { 'content-type': 'application/json' } }),
    )

    renderLogin()

    await userEvent.type(screen.getByLabelText(/account name/i), 'tom')
    await userEvent.type(screen.getByLabelText(/password/i), 'wrong')
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }))

    expect(await screen.findByText(/invalid/i)).toBeInTheDocument()
  })
})
