import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import '../../lib/i18n'
import { AssetSlotRow } from './AssetSlotRow'

function renderRow(url?: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <AssetSlotRow slot="logo" label="Logo" url={url} />
    </QueryClientProvider>,
  )
}

const settings = {
  description: null,
  contacts: { website: null, email: null, discord: null },
  registrationEnabled: true,
  assets: { logo: '/api/v1/server-info/assets/logo' },
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } })
}

describe('AssetSlotRow', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('previews an existing image and offers Remove', () => {
    renderRow('/api/v1/server-info/assets/logo')
    expect(screen.getByRole('img', { name: /logo/i })).toHaveAttribute(
      'src',
      expect.stringContaining('/api/v1/server-info/assets/logo'),
    )
    expect(screen.getByRole('button', { name: /remove/i })).toBeInTheDocument()
  })

  it('hides Remove when the slot is empty', () => {
    renderRow(undefined)
    expect(screen.queryByRole('button', { name: /remove/i })).not.toBeInTheDocument()
  })

  it('POSTs the chosen file to the slot', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(settings))
    renderRow(undefined)

    const file = new File(['x'], 'logo.png', { type: 'image/png' })
    await userEvent.upload(screen.getByTestId('asset-input-logo'), file)

    await waitFor(() => {
      const [url, init] = fetchSpy.mock.calls[0]
      expect(url).toBe('/api/v1/admin/server-settings/assets/logo')
      expect((init as RequestInit).method).toBe('POST')
      expect((init as RequestInit).body).toBeInstanceOf(FormData)
    })
  })

  it('DELETEs the slot on Remove', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 204 }))
    renderRow('/api/v1/server-info/assets/logo')

    await userEvent.click(screen.getByRole('button', { name: /remove/i }))

    await waitFor(() => {
      const [url, init] = fetchSpy.mock.calls[0]
      expect(url).toBe('/api/v1/admin/server-settings/assets/logo')
      expect((init as RequestInit).method).toBe('DELETE')
    })
  })
})
