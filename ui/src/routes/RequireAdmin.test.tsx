import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import '../lib/i18n'
import { AuthProvider } from '../lib/auth'
import { RequireAdmin } from './RequireAdmin'

function json(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } })
}

function futureIso() {
  return new Date(Date.now() + 3.6e6).toISOString()
}

function renderAt(level: string) {
  localStorage.setItem('mg-token', JSON.stringify({ token: 't', expiresAt: futureIso() }))
  vi.spyOn(globalThis, 'fetch').mockResolvedValue(json({ username: 'tom', level }))

  return render(
    <MemoryRouter initialEntries={['/admin']}>
      <AuthProvider>
        <Routes>
          <Route path="/" element={<p>dashboard</p>} />
          <Route
            path="/admin"
            element={
              <RequireAdmin>
                <p>admin content</p>
              </RequireAdmin>
            }
          />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  )
}

describe('RequireAdmin', () => {
  beforeEach(() => {
    localStorage.clear()
    sessionStorage.clear()
    vi.restoreAllMocks()
  })

  it('renders children for an admin session', async () => {
    renderAt('Administrator')
    expect(await screen.findByText('admin content')).toBeInTheDocument()
  })

  it('redirects a player to the dashboard', async () => {
    renderAt('Player')
    expect(await screen.findByText('dashboard')).toBeInTheDocument()
    expect(screen.queryByText('admin content')).not.toBeInTheDocument()
  })
})
