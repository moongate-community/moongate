import { ApiError, apiFetch, apiPath, apiStream, setAuthToken, setUnauthorizedHandler } from './api'

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

  it('lets the browser set the content-type for a FormData body', async () => {
    const fetchSpy = respond(200)
    const form = new FormData()
    form.append('file', new Blob(['x'], { type: 'image/png' }), 'logo.png')
    await apiFetch('/api/v1/admin/server-settings/assets/logo', { method: 'POST', body: form })

    const headers = new Headers((fetchSpy.mock.calls[0][1] as RequestInit).headers)
    expect(headers.has('content-type')).toBe(false)
  })

  it('still sets JSON content-type for a string body', async () => {
    const fetchSpy = respond(200)
    await apiFetch('/api/v1/x', { method: 'POST', body: JSON.stringify({ a: 1 }) })

    const headers = new Headers((fetchSpy.mock.calls[0][1] as RequestInit).headers)
    expect(headers.get('content-type')).toBe('application/json')
  })

  it('treats 202 Accepted as an empty body', async () => {
    // The console POST answers 202 with no body; parsing it as JSON would throw.
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 202 }))
    await expect(apiFetch<void>('/api/v1/admin/console', { method: 'POST', body: '{}' })).resolves.toBeUndefined()
  })

  it('apiStream sends the bearer and event-stream accept, returning the response', async () => {
    const streamResponse = new Response(new ReadableStream(), {
      status: 200,
      headers: { 'content-type': 'text/event-stream' },
    })
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(streamResponse)
    setAuthToken('tok')

    const response = await apiStream('/api/v1/admin/console/stream', new AbortController().signal)

    const headers = new Headers((fetchSpy.mock.calls[0][1] as RequestInit).headers)
    expect(headers.get('accept')).toBe('text/event-stream')
    expect(headers.get('authorization')).toBe('Bearer tok')
    expect(response).toBe(streamResponse)
  })

  it('apiStream runs the unauthorized handler on 401', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 401 }))
    const onUnauthorized = vi.fn()
    setUnauthorizedHandler(onUnauthorized)

    await expect(apiStream('/api/v1/admin/console/stream', new AbortController().signal)).rejects.toBeInstanceOf(
      ApiError,
    )
    expect(onUnauthorized).toHaveBeenCalledOnce()
  })
})
