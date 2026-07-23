// Transport only: no React, no cache, no component state. Keeping this layer ignorant of React is what
// makes the server-state layer above it replaceable without touching a line here.

export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

let authToken: string | null = null
let onUnauthorized: () => void = () => {}

export function setAuthToken(token: string | null): void {
  authToken = token
}

/** Called on any 401 — one place, so no caller has to remember to handle an expired session. */
export function setUnauthorizedHandler(handler: () => void): void {
  onUnauthorized = handler
}

/** Fills `{placeholders}` in a route template, encoding each value. */
export function apiPath(template: string, params: Record<string, string>): string {
  return template.replace(/\{(\w+)\}/g, (_, key: string) => encodeURIComponent(params[key] ?? ''))
}

export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('accept', 'application/json')

  // A FormData body must keep the content-type the browser assigns, boundary and all; only a
  // hand-built (JSON) body gets the JSON type.
  if (init.body !== undefined && !headers.has('content-type') && !(init.body instanceof FormData)) {
    headers.set('content-type', 'application/json')
  }

  if (authToken !== null) {
    headers.set('authorization', `Bearer ${authToken}`)
  }

  const response = await fetch(path, { ...init, headers })

  if (response.status === 401) {
    onUnauthorized()
    throw new ApiError(401, 'unauthorized')
  }

  if (!response.ok) {
    throw new ApiError(response.status, `${init.method ?? 'GET'} ${path} failed with ${response.status}`)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}
