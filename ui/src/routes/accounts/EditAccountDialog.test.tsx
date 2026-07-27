import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import '../../lib/i18n'
import { EditAccountDialog } from './EditAccountDialog'
import type { Account } from '../../lib/accounts'

const account: Account = { username: 'grimble', email: null, level: 'Player', isActive: true, characterCount: 1 }

function renderDialog() {
  const client = new QueryClient({ defaultOptions: { mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <EditAccountDialog account={account} onOpenChange={() => {}} />
    </QueryClientProvider>,
  )
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } })
}

describe('EditAccountDialog', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('saves the suspended state via PATCH', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(json({ ...account, isActive: false }))
    renderDialog()

    await userEvent.click(screen.getByRole('switch', { name: /suspended/i }))
    await userEvent.click(screen.getByRole('button', { name: /save/i }))

    await waitFor(() => {
      const [url, init] = fetchSpy.mock.calls[0]
      expect(url).toBe('/api/v1/admin/accounts/grimble')
      expect((init as RequestInit).method).toBe('PATCH')
      expect(JSON.parse((init as RequestInit).body as string)).toMatchObject({ isActive: false })
    })
  })

  it('deletes after confirming', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 204 }))
    renderDialog()

    await userEvent.click(screen.getByRole('button', { name: /delete account/i }))
    await userEvent.click(screen.getByRole('button', { name: /^delete$/i }))

    await waitFor(() => {
      const [url, init] = fetchSpy.mock.calls[0]
      expect(url).toBe('/api/v1/admin/accounts/grimble')
      expect((init as RequestInit).method).toBe('DELETE')
    })
  })
})
