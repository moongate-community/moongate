import { renderHook } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import {
  useRegisterAccount,
  useResendVerification,
  useVerifyRegistration,
  validateRegistration,
  validateResend,
} from './registration'

function wrapper() {
  const client = new QueryClient({ defaultOptions: { mutations: { retry: false } } })
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  )
}

describe('registration data module', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('reports independently derived registration validation errors', () => {
    expect(
      validateRegistration({
        username: 'ab',
        email: 'bad',
        password: 'short',
        confirmation: 'different',
      }),
    ).toEqual({
      username: 'invalid',
      email: 'invalid',
      password: 'invalid',
      confirmation: 'mismatch',
    })
  })

  it.each([
    ['username shorter than three characters', { username: 'ab' }, { username: 'invalid' }],
    ['username longer than thirty characters', { username: 'a'.repeat(31) }, { username: 'invalid' }],
    ['username containing a non-ASCII character', { username: 'naïve' }, { username: 'invalid' }],
    ['password shorter than eight characters', { password: 'pass123' }, { password: 'invalid' }],
    ['password longer than thirty characters', { password: 'a'.repeat(31) }, { password: 'invalid' }],
    ['password containing a control character', { password: 'pass\nword' }, { password: 'invalid' }],
    ['password containing a non-ASCII character', { password: 'pässword' }, { password: 'invalid' }],
  ])('rejects %s', (_, changes, expected) => {
    const data = {
      username: 'newbie',
      email: 'newbie@example.test',
      password: 'password',
      confirmation: 'password',
      ...changes,
      ...('password' in changes ? { confirmation: changes.password } : {}),
    }

    expect(validateRegistration(data)).toEqual(expected)
  })

  it('trims username and email but preserves the password', () => {
    expect(
      validateRegistration({
        username: '  newbie  ',
        email: '  newbie@example.test  ',
        password: ' pass word ',
        confirmation: ' pass word ',
      }),
    ).toEqual({})
  })

  it('validates the resend identity with the same trimmed username and email rules', () => {
    expect(validateResend({ username: '  ab  ', email: 'bad' })).toEqual({ username: 'invalid', email: 'invalid' })
    expect(validateResend({ username: '  newbie  ', email: '  newbie@example.test  ' })).toEqual({})
  })

  it('useRegisterAccount POSTs the generated request body and resolves void', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 202 }))
    const { result } = renderHook(() => useRegisterAccount(), { wrapper: wrapper() })

    await expect(
      result.current.mutateAsync({ username: 'newbie', password: 'password', email: 'newbie@example.test' }),
    ).resolves.toBeUndefined()

    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe('/api/v1/register')
    expect((init as RequestInit).method).toBe('POST')
    expect(JSON.parse((init as RequestInit).body as string)).toEqual({
      username: 'newbie',
      password: 'password',
      email: 'newbie@example.test',
    })
  })

  it('useResendVerification POSTs the generated request body and resolves void', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 202 }))
    const { result } = renderHook(() => useResendVerification(), { wrapper: wrapper() })

    await expect(
      result.current.mutateAsync({ username: 'newbie', email: 'newbie@example.test' }),
    ).resolves.toBeUndefined()

    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe('/api/v1/register/resend')
    expect((init as RequestInit).method).toBe('POST')
    expect(JSON.parse((init as RequestInit).body as string)).toEqual({
      username: 'newbie',
      email: 'newbie@example.test',
    })
  })

  it('useVerifyRegistration POSTs the generated request body and resolves void', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 200 }))
    const { result } = renderHook(() => useVerifyRegistration(), { wrapper: wrapper() })

    await expect(result.current.mutateAsync({ token: 'verification-token' })).resolves.toBeUndefined()

    const [url, init] = fetchSpy.mock.calls[0]
    expect(url).toBe('/api/v1/register/verify')
    expect((init as RequestInit).method).toBe('POST')
    expect(JSON.parse((init as RequestInit).body as string)).toEqual({ token: 'verification-token' })
  })
})
