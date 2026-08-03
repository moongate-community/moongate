import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'

import '../../lib/i18n'
import { TooltipProvider } from '../../components/ui/tooltip'
import { ItemTemplatesScreen } from './ItemTemplatesScreen'
import type { ItemTemplatePage, ItemTemplateSummary } from '../../lib/templates'

const query = vi.hoisted(() => ({
  data: undefined as ItemTemplatePage | undefined,
  isPending: false,
  isError: false,
  lastParams: null as { page: number; search: string } | null,
}))

vi.mock('../../lib/templates', async (original) => ({
  ...(await original<typeof import('../../lib/templates')>()),
  useItemTemplates: (params: { page: number; search: string }) => {
    query.lastParams = params
    return query
  },
}))

function template(id: string, name: string, over: Partial<ItemTemplateSummary> = {}): ItemTemplateSummary {
  return {
    id,
    name,
    category: 'armor',
    itemId: 0x1415,
    hue: 0,
    rarity: 'Rare',
    goldValue: 250,
    weight: 40,
    tags: ['armor'],
    imageUrl: `/api/v1/images/items/0x1415.png`,
    ...over,
  } as ItemTemplateSummary
}

function page(items: ItemTemplateSummary[], over: Partial<ItemTemplatePage> = {}): ItemTemplatePage {
  return { items, total: items.length, page: 1, pageSize: 25, totalPages: 1, ...over } as ItemTemplatePage
}

function renderWith(data: ItemTemplatePage | undefined, state: Partial<typeof query> = {}) {
  Object.assign(query, { data, isPending: false, isError: false }, state)

  return render(
    <MemoryRouter>
      <TooltipProvider>
        <ItemTemplatesScreen />
      </TooltipProvider>
    </MemoryRouter>,
  )
}

describe('ItemTemplatesScreen', () => {
  it('lists the templates', () => {
    renderWith(page([template('plate_mail', 'Plate Mail'), template('dagger', 'Dagger')]))

    expect(screen.getByText('Plate Mail')).toBeInTheDocument()
    expect(screen.getByText('Dagger')).toBeInTheDocument()
  })

  it('shows the sprite the row carries', () => {
    renderWith(page([template('plate_mail', 'Plate Mail')]))

    expect(screen.getByAltText('Plate Mail')).toHaveAttribute('src', '/api/v1/images/items/0x1415.png')
  })

  it('links each template to its detail page', () => {
    renderWith(page([template('plate_mail', 'Plate Mail')]))

    expect(screen.getByRole('link', { name: 'Plate Mail' })).toHaveAttribute('href', '/admin/items/plate_mail')
  })

  // The search is the server's. If this ever asserts filtered rows instead of the passed-through
  // term, the screen has started filtering locally and searches a single page.
  it('hands the typed search to the query', async () => {
    renderWith(page([template('plate_mail', 'Plate Mail')]))

    await userEvent.type(screen.getByPlaceholderText(/Search by id/), 'dag')

    expect(query.lastParams?.search).toBe('dag')
  })

  it('asks for the next page', async () => {
    renderWith(page([template('plate_mail', 'Plate Mail')], { totalPages: 3 }))

    await userEvent.click(screen.getByRole('button', { name: /next/i }))

    expect(query.lastParams?.page).toBe(2)
  })

  // Searching from page 3 would otherwise land on page 3 of a one-page result: an empty table that
  // reads as "no matches" for a search that in fact matched.
  it('returns to the first page when the search changes', async () => {
    renderWith(page([template('plate_mail', 'Plate Mail')], { totalPages: 3 }))

    await userEvent.click(screen.getByRole('button', { name: /next/i }))
    expect(query.lastParams?.page).toBe(2)

    await userEvent.type(screen.getByPlaceholderText(/Search by id/), 'd')

    expect(query.lastParams?.page).toBe(1)
  })

  it('reports the total, which counts every match and not just this page', () => {
    renderWith(page([template('plate_mail', 'Plate Mail')], { total: 87, totalPages: 4 }))

    // The total is its own stat card now, counting every match rather than the rows on this page.
    expect(screen.getByText('87')).toBeInTheDocument()
  })

  it('says so when a search matches nothing', () => {
    renderWith(page([], { total: 0 }))

    expect(screen.getByText('No template matches that search.')).toBeInTheDocument()
  })

  it('says so when the request failed', () => {
    renderWith(undefined, { isError: true })

    expect(screen.getByRole('alert')).toHaveTextContent('The templates could not be loaded.')
  })

  // Templates are named by cliloc when they carry no name of their own, and the server already
  // resolves that — but an empty one must still be reachable.
  it('falls back to the id for a template with no name', () => {
    renderWith(page([template('mystery', '')]))

    expect(screen.getByRole('link', { name: 'mystery' })).toBeInTheDocument()
  })
})
