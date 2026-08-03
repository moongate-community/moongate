import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import '../../lib/i18n'
import { AccountDetailPanel } from './AccountDetailPanel'
import type { Account } from '../../lib/accounts'

const account: Account = { username: 'grimble', email: null, level: 'Player', isActive: true, characterCount: 1 }

function renderPanel(shown: Account | null = account) {
  const client = new QueryClient({ defaultOptions: { mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <AccountDetailPanel account={shown} />
    </QueryClientProvider>,
  )
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } })
}

describe('AccountDetailPanel', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('asks for a pick when nothing is selected', () => {
    renderPanel(null)

    expect(screen.getByText(/pick an account/i)).toBeInTheDocument()
  })

  it('names the account and shows its standing', () => {
    renderPanel()

    expect(screen.getByText('grimble')).toBeInTheDocument()
    expect(screen.getByText('active')).toBeInTheDocument()
    expect(screen.getByText('1 character')).toBeInTheDocument()
    // 'Player' is both the badge and the level the form is set to, so this names which one it means.
    expect(screen.getByRole('combobox')).toHaveTextContent('Player')
  })

  it('saves the suspended state via PATCH', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(json({ ...account, isActive: false }))
    renderPanel()

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
    renderPanel()

    await userEvent.click(screen.getByRole('button', { name: /delete account/i }))
    await userEvent.click(screen.getByRole('button', { name: /^delete$/i }))

    await waitFor(() => {
      const [url, init] = fetchSpy.mock.calls[0]
      expect(url).toBe('/api/v1/admin/accounts/grimble')
      expect((init as RequestInit).method).toBe('DELETE')
    })
  })

  // The list refetches on its own -- on window focus, after a save elsewhere. Each refetch hands the
  // panel a new object for the same account, and that must not throw away what staff is halfway
  // through typing.
  it('keeps a level the staff picked when the list refetches', async () => {
    const { rerender } = renderPanel()

    await userEvent.click(screen.getByRole('combobox'))
    await userEvent.click(screen.getByRole('option', { name: 'Administrator' }))
    expect(screen.getByRole('combobox')).toHaveTextContent('Administrator')

    const client = new QueryClient({ defaultOptions: { mutations: { retry: false } } })
    rerender(
      <QueryClientProvider client={client}>
        <AccountDetailPanel account={{ ...account }} />
      </QueryClientProvider>,
    )

    expect(screen.getByRole('combobox')).toHaveTextContent('Administrator')
  })

  // Picking a second account while the first one's form is dirty must not carry the edit across.
  it('starts over when a different account is picked', async () => {
    const { rerender } = renderPanel()

    await userEvent.click(screen.getByRole('switch', { name: /suspended/i }))
    expect(screen.getByRole('switch', { name: /suspended/i })).toBeChecked()

    const client = new QueryClient({ defaultOptions: { mutations: { retry: false } } })
    rerender(
      <QueryClientProvider client={client}>
        <AccountDetailPanel account={{ ...account, username: 'thorne' }} />
      </QueryClientProvider>,
    )

    expect(screen.getByRole('switch', { name: /suspended/i })).not.toBeChecked()
  })
})
