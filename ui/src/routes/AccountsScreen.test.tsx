import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import '../lib/i18n'
import { AccountsScreen } from './AccountsScreen'

function renderScreen() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <AccountsScreen />
    </QueryClientProvider>,
  )
}

function json(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } })
}

const accounts = [
  { username: 'aelric', email: 'a@x.io', level: 'Administrator', isActive: true, characterCount: 3 },
  { username: 'grimble', email: null, level: 'Player', isActive: false, characterCount: 1 },
]

describe('AccountsScreen', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('lists accounts and filters by search', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(accounts))
    renderScreen()

    expect(await screen.findByText('aelric')).toBeInTheDocument()
    expect(screen.getByText('grimble')).toBeInTheDocument()

    await userEvent.type(screen.getByPlaceholderText(/search/i), 'aelr')
    expect(screen.queryByText('grimble')).not.toBeInTheDocument()
    expect(screen.getByText('aelric')).toBeInTheDocument()
  })

  it('opens the create dialog from the New button', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(accounts))
    renderScreen()
    await screen.findByText('aelric')

    await userEvent.click(screen.getByRole('button', { name: /new account/i }))
    expect(await screen.findByRole('dialog')).toBeInTheDocument()
  })
})
