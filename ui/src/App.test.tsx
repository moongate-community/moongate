import { render, screen } from '@testing-library/react'
import i18n from './lib/i18n'
import { App } from './App'

function json(body: unknown) {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'content-type': 'application/json' },
  })
}

function servePublicApi() {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const url = String(input)

    if (url.endsWith('/api/v1/server-info')) {
      return json({
        shardName: 'Moongate',
        description: null,
        tagline: null,
        contacts: { website: null, email: null, discord: null },
        registrationEnabled: true,
        assets: {},
      })
    }

    if (url.endsWith('/api/v1/stats')) {
      return json({
        generatedAt: '2026-07-25T00:00:00Z',
        uptimeSeconds: 1,
        players: { online: 0, connections: 0 },
        accounts: { total: 0, active: 0, characters: 0 },
        world: { npcs: 0, items: 0 },
        content: {},
      })
    }

    if (url.endsWith('/api/v1/version')) {
      return json({ shardName: 'Moongate', version: '1.0.0' })
    }

    return json({})
  })
}

describe('App public registration routes', () => {
  beforeEach(async () => {
    localStorage.clear()
    sessionStorage.clear()
    vi.restoreAllMocks()
    await i18n.changeLanguage('en')
  })

  it.each([
    ['/register', /create account/i],
    ['/register/pending', /check your email/i],
    ['/verify', /verification link invalid/i],
  ])('routes %s to its public screen', async (path, expected) => {
    const fetchSpy = servePublicApi()
    window.history.pushState({}, '', path)

    render(<App />)

    expect(await screen.findByRole('heading', { name: expected })).toBeInTheDocument()
    if (path === '/verify') {
      expect(
        fetchSpy.mock.calls.filter(
          ([input, init]) =>
            String(input).endsWith('/api/v1/register/verify') && (init as RequestInit | undefined)?.method === 'POST',
        ),
      ).toHaveLength(0)
    }
  })
})
