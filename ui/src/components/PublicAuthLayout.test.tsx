import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, Route, Routes } from 'react-router'
import '../lib/i18n'
import { AuthProvider } from '../lib/auth'
import { PublicAuthLayout } from './PublicAuthLayout'

function json(body: unknown) {
  return new Response(JSON.stringify(body), {
    headers: { 'content-type': 'application/json' },
  })
}

function serveApi() {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const url = String(input)

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
        tagline: null,
        contacts: { website: null, email: null, discord: null },
        registrationEnabled: false,
        assets: { Logo: '/api/v1/server-info/assets/logo' },
      })
    }
    return json({ username: 'tom', level: 'Player' })
  })
}

function renderLayout() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })

  return render(
    <MemoryRouter initialEntries={['/login']}>
      <QueryClientProvider client={client}>
        <AuthProvider>
          <Routes>
            <Route
              path="/login"
              element={
                <PublicAuthLayout>
                  <p>form content</p>
                </PublicAuthLayout>
              }
            />
            <Route path="/" element={<p>dashboard</p>} />
          </Routes>
        </AuthProvider>
      </QueryClientProvider>
    </MemoryRouter>,
  )
}

describe('PublicAuthLayout', () => {
  beforeEach(() => {
    localStorage.clear()
    sessionStorage.clear()
    vi.restoreAllMocks()
  })

  it('renders public shard details around its form content', async () => {
    serveApi()
    renderLayout()

    expect(await screen.findByRole('img', { name: /shard logo/i })).toHaveAttribute(
      'src',
      expect.stringContaining('/api/v1/server-info/assets/logo'),
    )
    expect(screen.getByText(/Sosaria never sleeps\./)).toBeInTheDocument()
    expect(screen.getByText(/184 adventurers online right now/)).toBeInTheDocument()
    expect(screen.getByText(/Moongate · v9\.9\.9/)).toBeInTheDocument()
    expect(screen.getByText('form content')).toBeInTheDocument()
  })

  it('redirects an authenticated session to the dashboard', async () => {
    localStorage.setItem(
      'mg-token',
      JSON.stringify({ token: 't', expiresAt: new Date(Date.now() + 3.6e6).toISOString() }),
    )
    serveApi()
    renderLayout()

    expect(await screen.findByText('dashboard')).toBeInTheDocument()
    expect(screen.queryByText('form content')).not.toBeInTheDocument()
  })
})
