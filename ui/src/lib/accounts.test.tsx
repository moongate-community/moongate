import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { useAccounts, useCreateAccount, useDeleteAccount, useUpdateAccount } from './accounts'

function wrapper() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  )
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } })
}

describe('accounts data module', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('useAccounts reads the list', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      json([{ username: 'tom', email: null, level: 'Player', isActive: true, characterCount: 2 }]),
    )
    const { result } = renderHook(() => useAccounts(), { wrapper: wrapper() })
    await waitFor(() => expect(result.current.data?.[0].username).toBe('tom'))
  })

  it('useCreateAccount POSTs the body', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      json({ username: 'new', email: null, level: 'Player', isActive: true, characterCount: 0 }, 201),
    )
    const { result } = renderHook(() => useCreateAccount(), { wrapper: wrapper() })
    await result.current.mutateAsync({ username: 'new', password: 'pw' })

    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe('/api/v1/admin/accounts')
    expect((init as RequestInit).method).toBe('POST')
    expect(JSON.parse((init as RequestInit).body as string)).toMatchObject({ username: 'new', password: 'pw' })
  })

  it('useUpdateAccount PATCHes the named account', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      json({ username: 'tom', email: null, level: 'Administrator', isActive: false, characterCount: 2 }),
    )
    const { result } = renderHook(() => useUpdateAccount(), { wrapper: wrapper() })
    await result.current.mutateAsync({ username: 'tom', patch: { isActive: false } })

    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe('/api/v1/admin/accounts/tom')
    expect((init as RequestInit).method).toBe('PATCH')
    expect(JSON.parse((init as RequestInit).body as string)).toEqual({ isActive: false })
  })

  it('useDeleteAccount DELETEs the named account', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 204 }))
    const { result } = renderHook(() => useDeleteAccount(), { wrapper: wrapper() })
    await result.current.mutateAsync('tom')

    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe('/api/v1/admin/accounts/tom')
    expect((init as RequestInit).method).toBe('DELETE')
  })
})
