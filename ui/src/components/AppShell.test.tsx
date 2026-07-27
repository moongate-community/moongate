import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import '../lib/i18n'
import { AppShell } from './AppShell'
import { AuthProvider } from '../lib/auth'

function renderShell() {
  return render(
    <MemoryRouter>
      <AuthProvider>
        <AppShell>
          <p>content</p>
        </AppShell>
      </AuthProvider>
    </MemoryRouter>,
  )
}

function json(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } })
}

function renderWithLevel(level: string) {
  localStorage.setItem(
    'mg-token',
    JSON.stringify({ token: 't', expiresAt: new Date(Date.now() + 3.6e6).toISOString() }),
  )
  vi.spyOn(globalThis, 'fetch').mockResolvedValue(json({ username: 'tom', level }))

  return render(
    <MemoryRouter>
      <AuthProvider>
        <AppShell>
          <p>content</p>
        </AppShell>
      </AuthProvider>
    </MemoryRouter>,
  )
}

describe('AppShell', () => {
  beforeEach(() => {
    localStorage.clear()
    sessionStorage.clear()
    vi.restoreAllMocks()
  })

  it('shows the brand, the tab row and the routed content', () => {
    renderShell()

    expect(screen.getByText('Moongate')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /dashboard/i })).toBeInTheDocument()
    expect(screen.getByText('content')).toBeInTheDocument()
  })

  it('offers the theme toggle', () => {
    renderShell()
    // Dark is the default, so the toggle offers the switch to light.
    expect(screen.getByRole('button', { name: /light/i })).toBeInTheDocument()
  })

  it('shows the Admin tab for an admin session', async () => {
    renderWithLevel('Administrator')
    expect(await screen.findByRole('link', { name: /admin/i })).toBeInTheDocument()
  })

  it('hides the Admin tab from a player', async () => {
    renderWithLevel('Player')
    await waitFor(() => expect(screen.getByText('tom')).toBeInTheDocument())
    expect(screen.queryByRole('link', { name: /admin/i })).not.toBeInTheDocument()
  })
})
