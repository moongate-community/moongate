import { ApiError, apiFetch, apiPath, setAuthToken, setUnauthorizedHandler } from './api'

describe('apiFetch', () => {
  beforeEach(() => {
    setAuthToken(null)
    setUnauthorizedHandler(() => {})
    vi.restoreAllMocks()
  })

  function respond(status: number, body: unknown = {}) {
    return vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } }),
    )
  }

  it('returns the parsed body on success', async () => {
    respond(200, { username: 'tom' })
    await expect(apiFetch<{ username: string }>('/api/v1/player/me')).resolves.toEqual({ username: 'tom' })
  })

  it('sends the bearer token once one is set', async () => {
    const fetchSpy = respond(200)
    setAuthToken('abc')
    await apiFetch('/api/v1/player/me')

    const headers = new Headers((fetchSpy.mock.calls[0][1] as RequestInit).headers)
    expect(headers.get('authorization')).toBe('Bearer abc')
  })

  it('throws ApiError carrying the status', async () => {
    respond(404, { title: 'Not Found' })
    await expect(apiFetch('/api/v1/nope')).rejects.toMatchObject({ status: 404 })
    await expect(apiFetch('/api/v1/nope')).rejects.toBeInstanceOf(ApiError)
  })

  it('runs the unauthorized handler on 401 and still throws', async () => {
    respond(401)
    const onUnauthorized = vi.fn()
    setUnauthorizedHandler(onUnauthorized)

    await expect(apiFetch('/api/v1/player/me')).rejects.toBeInstanceOf(ApiError)
    expect(onUnauthorized).toHaveBeenCalledOnce()
  })

  it('encodes path parameters', () => {
    // Without encoding, an account named `bob#x` would address `bob` and the operation would silently hit
    // the wrong record.
    expect(apiPath('/api/v1/admin/accounts/{username}', { username: 'bob#x' })).toBe(
      '/api/v1/admin/accounts/bob%23x',
    )
  })
})
