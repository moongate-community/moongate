import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { useDeleteAsset, useServerSettings, useUpdateSettings, useUploadAsset } from './settings'

function wrapper() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  )
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } })
}

const settings = {
  description: 'A shard',
  contacts: { website: null, email: null, discord: null },
  registrationEnabled: true,
  assets: {},
}

describe('settings data module', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('useServerSettings reads the settings', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(settings))
    const { result } = renderHook(() => useServerSettings(), { wrapper: wrapper() })
    await waitFor(() => expect(result.current.data?.description).toBe('A shard'))
  })

  it('useUpdateSettings PUTs the body', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(settings))
    const { result } = renderHook(() => useUpdateSettings(), { wrapper: wrapper() })
    await result.current.mutateAsync({ registrationEnabled: false })

    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe('/api/v1/admin/server-settings')
    expect((init as RequestInit).method).toBe('PUT')
    expect(JSON.parse((init as RequestInit).body as string)).toEqual({ registrationEnabled: false })
  })

  it('useUploadAsset posts a FormData to the slot', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(settings))
    const { result } = renderHook(() => useUploadAsset(), { wrapper: wrapper() })
    const file = new File(['x'], 'logo.png', { type: 'image/png' })
    await result.current.mutateAsync({ slot: 'logo', file })

    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe('/api/v1/admin/server-settings/assets/logo')
    expect((init as RequestInit).method).toBe('POST')
    expect((init as RequestInit).body).toBeInstanceOf(FormData)
  })

  it('useDeleteAsset deletes the slot', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 204 }))
    const { result } = renderHook(() => useDeleteAsset(), { wrapper: wrapper() })
    await result.current.mutateAsync('favicon')

    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe('/api/v1/admin/server-settings/assets/favicon')
    expect((init as RequestInit).method).toBe('DELETE')
  })
})
