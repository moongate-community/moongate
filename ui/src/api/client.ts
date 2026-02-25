import { useAuthStore } from '../store/authStore'

const BASE = '/api'

function getHeaders(extra?: HeadersInit): HeadersInit {
  const token = useAuthStore.getState().user?.accessToken
  return {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...(extra as Record<string, string> ?? {}),
  }
}

export async function apiFetch<T>(
  path: string,
  options?: RequestInit,
): Promise<T> {
  const url = path.startsWith('/auth') ? path : `${BASE}${path}`
  const res = await fetch(url, {
    ...options,
    headers: getHeaders(options?.headers),
  })

  if (!res.ok) {
    const text = await res.text()
    throw new Error(text || `HTTP ${res.status}`)
  }

  const contentType = res.headers.get('content-type') ?? ''
  if (contentType.includes('application/json')) {
    return res.json() as Promise<T>
  }

  return res.text() as unknown as Promise<T>
}

export const api = {
  get: <T>(path: string) => apiFetch<T>(path),
  post: <T>(path: string, body: unknown) =>
    apiFetch<T>(path, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  put: <T>(path: string, body: unknown) =>
    apiFetch<T>(path, {
      method: 'PUT',
      body: JSON.stringify(body),
    }),
  delete: <T>(path: string) =>
    apiFetch<T>(path, {
      method: 'DELETE',
    }),
}
