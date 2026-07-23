import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import '../../lib/i18n'
import { NewAccountDialog } from './NewAccountDialog'

function renderDialog() {
  const client = new QueryClient({ defaultOptions: { mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <NewAccountDialog open onOpenChange={() => {}} />
    </QueryClientProvider>,
  )
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } })
}

describe('NewAccountDialog', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('posts the new account', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(json({ username: 'new', email: null, level: 'Player', isActive: true, characterCount: 0 }, 201))
    renderDialog()

    await userEvent.type(screen.getByLabelText(/username/i), 'new')
    await userEvent.type(screen.getByLabelText(/password/i), 'secret')
    await userEvent.click(screen.getByRole('button', { name: /create/i }))

    await waitFor(() =>
      expect(fetchSpy).toHaveBeenCalledWith('/api/v1/admin/accounts', expect.objectContaining({ method: 'POST' })),
    )
  })

  it('shows a message when the username is taken', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(json({ title: 'Conflict' }, 409))
    renderDialog()

    await userEvent.type(screen.getByLabelText(/username/i), 'tom')
    await userEvent.type(screen.getByLabelText(/password/i), 'secret')
    await userEvent.click(screen.getByRole('button', { name: /create/i }))

    expect(await screen.findByText(/already taken/i)).toBeInTheDocument()
  })
})
