import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import '../../lib/i18n'
import { EntityPicker } from './entity-picker'
import { CATALOGUES } from '../../lib/catalogues'

function page(items: unknown[]) {
  return new Response(JSON.stringify({ items, total: items.length, page: 1, pageSize: 25, totalPages: 1 }), {
    status: 200,
    headers: { 'content-type': 'application/json' },
  })
}

const template = { id: 'dagger', name: 'a dagger', imageUrl: '/api/v1/images/items/0x0F51.png' }
const hue = { value: 1002, name: 'blood red', hex: '#8B0000', gradient: [] }

function renderPicker(onSelect = vi.fn()) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })

  render(
    <QueryClientProvider client={client}>
      <EntityPicker open onOpenChange={() => {}} onSelect={onSelect} />
    </QueryClientProvider>,
  )

  return onSelect
}

describe('EntityPicker', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('opens on the first catalogue and lists what it holds', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(page([template]))
    renderPicker()

    expect(await screen.findByText('a dagger')).toBeInTheDocument()
  })

  it('offers every catalogue', () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(page([]))
    renderPicker()

    for (const label of ['Item templates', 'Mobile templates', 'UO items', 'Hues', 'Bodies', 'Hair styles']) {
      expect(screen.getByRole('button', { name: label })).toBeInTheDocument()
    }
  })

  // Each catalogue has its own route, so the mock answers by route: anything else would feed rows of
  // one shape to the reader of another, which cannot happen against the real server.
  it('reads the catalogue that was chosen', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((input) =>
      Promise.resolve(String(input).includes('/hues') ? page([hue]) : page([template])),
    )
    renderPicker()

    expect(await screen.findByText('a dagger')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Hues' }))

    expect(await screen.findByText('blood red')).toBeInTheDocument()
    expect(screen.queryByText('a dagger')).not.toBeInTheDocument()
  })

  // A search typed for templates means nothing among hues, and carrying it over would open the next
  // catalogue on an empty list for no visible reason.
  it('clears the search when the catalogue changes', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(page([]))
    renderPicker()

    await userEvent.type(screen.getByLabelText(/search the catalogue/i), 'dagger')
    await waitFor(() => expect(String(fetchSpy.mock.calls.at(-1)?.[0])).toContain('search=dagger'))

    await userEvent.click(screen.getByRole('button', { name: 'Hues' }))

    expect(screen.getByLabelText(/search the catalogue/i)).toHaveValue('')
    await waitFor(() => expect(String(fetchSpy.mock.calls.at(-1)?.[0])).not.toContain('search'))
  })

  it('hands back the entry that was picked', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(page([template]))
    const onSelect = renderPicker()

    await userEvent.click(await screen.findByRole('button', { name: /a dagger/ }))

    expect(onSelect).toHaveBeenCalledWith(
      expect.objectContaining({ value: 'dagger', label: 'a dagger' }),
      expect.objectContaining({ id: 'itemTemplates' }),
    )
  })

  it('offers the flag filter only on the raw tiles', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(page([]))
    renderPicker()

    expect(screen.queryByLabelText(/filter by flag/i)).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'UO items' }))

    expect(screen.getByLabelText(/filter by flag/i)).toBeInTheDocument()
  })

  it('sends the flag that was filtered by', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(page([]))
    renderPicker()

    await userEvent.click(screen.getByRole('button', { name: 'UO items' }))
    await userEvent.selectOptions(screen.getByLabelText(/filter by flag/i), 'Container')

    await waitFor(() => expect(String(fetchSpy.mock.calls.at(-1)?.[0])).toContain('flag=Container'))
  })

  it('says so when nothing matches', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(page([]))
    renderPicker()

    expect(await screen.findByText(/nothing matches/i)).toBeInTheDocument()
  })

  // The width is invisible to jsdom, but the trap that swallowed it is not: DialogContent caps
  // itself at sm:max-w-lg, and undoing only the unprefixed max-width leaves that cap standing on
  // every screen wider than 640px -- which is every screen this is used on.
  it('undoes the dialog cap that would otherwise decide its width', () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(page([]))
    renderPicker()

    const dialog = screen.getByRole('dialog')

    expect(dialog.className).toContain('sm:max-w-none')
    expect(dialog.className).toContain('md:w-[60vw]')
  })

  it('can be narrowed to one catalogue', () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(page([]))
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })

    render(
      <QueryClientProvider client={client}>
        <EntityPicker
          open
          onOpenChange={() => {}}
          onSelect={() => {}}
          catalogues={CATALOGUES.filter((catalogue) => catalogue.id === 'hues')}
        />
      </QueryClientProvider>,
    )

    expect(screen.getByRole('button', { name: 'Hues' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Item templates' })).not.toBeInTheDocument()
  })
})
