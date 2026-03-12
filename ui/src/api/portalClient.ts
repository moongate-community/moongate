import { usePortalAuthStore } from '../store/portalAuthStore'

const BASE = '/api'

function resolveApiUrl(path: string): string {
  if (
    path.startsWith('/api/') ||
    path.startsWith('/auth') ||
    path.startsWith('/health') ||
    path.startsWith('/metrics') ||
    path.startsWith('/openapi') ||
    path.startsWith('/scalar')
  ) {
    return path
  }

  return `${BASE}${path}`
}

function getHeaders(extra?: HeadersInit): HeadersInit {
  const token = usePortalAuthStore.getState().user?.accessToken
  return {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...((extra as Record<string, string> | undefined) ?? {}),
  }
}

export async function portalApiFetch<T>(
  path: string,
  options?: RequestInit,
): Promise<T> {
  const res = await rawPortalApiFetch(path, options)

  const contentType = res.headers.get('content-type') ?? ''
  if (contentType.includes('application/json')) {
    return res.json() as Promise<T>
  }

  return res.text() as unknown as Promise<T>
}

export async function rawPortalApiFetch(
  path: string,
  options?: RequestInit,
): Promise<Response> {
  const url = resolveApiUrl(path)
  const res = await fetch(url, {
    ...options,
    headers: getHeaders(options?.headers),
  })

  if (!res.ok) {
    const text = await res.text()
    throw new Error(text || `HTTP ${res.status}`)
  }

  return res
}

export const portalApi = {
  get: <T>(path: string) => portalApiFetch<T>(path),
  put: <T>(path: string, body: unknown) =>
    portalApiFetch<T>(path, {
      method: 'PUT',
      body: JSON.stringify(body),
    }),
  post: <T>(path: string, body: unknown) =>
    portalApiFetch<T>(path, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
}
