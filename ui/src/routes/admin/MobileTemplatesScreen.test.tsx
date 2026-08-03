import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'

import '../../lib/i18n'
import { TooltipProvider } from '../../components/ui/tooltip'
import { MobileTemplatesScreen } from './MobileTemplatesScreen'
import type { MobileTemplatePage, MobileTemplateSummary } from '../../lib/templates'

const query = vi.hoisted(() => ({
  data: undefined as MobileTemplatePage | undefined,
  isPending: false,
  isError: false,
  lastParams: null as { page: number; search: string } | null,
}))

vi.mock('../../lib/templates', async (original) => ({
  ...(await original<typeof import('../../lib/templates')>()),
  useMobileTemplates: (params: { page: number; search: string }) => {
    query.lastParams = params
    return query
  },
}))

function template(id: string, name: string, over: Partial<MobileTemplateSummary> = {}): MobileTemplateSummary {
  return {
    id,
    name,
    title: 'the guard',
    category: 'npc',
    tags: ['npc'],
    body: 400,
    strength: 96,
    dexterity: 70,
    intelligence: 30,
    variantCount: 2,
    imageUrl: `/api/v1/images/mobiles/templates/${id}.png`,
    ...over,
  } as MobileTemplateSummary
}

function page(items: MobileTemplateSummary[], over: Partial<MobileTemplatePage> = {}): MobileTemplatePage {
  return { items, total: items.length, page: 1, pageSize: 25, totalPages: 1, ...over } as MobileTemplatePage
}

function renderWith(data: MobileTemplatePage | undefined, state: Partial<typeof query> = {}) {
  Object.assign(query, { data, isPending: false, isError: false }, state)

  return render(
    <MemoryRouter>
      <TooltipProvider>
        <MobileTemplatesScreen />
      </TooltipProvider>
    </MemoryRouter>,
  )
}

describe('MobileTemplatesScreen', () => {
  it('lists the templates', () => {
    renderWith(page([template('warrior_guard', 'a guard'), template('brigand', 'a brigand')]))

    expect(screen.getByText('a guard')).toBeInTheDocument()
    expect(screen.getByText('a brigand')).toBeInTheDocument()
  })

  it('shows the sprite the row carries', () => {
    renderWith(page([template('warrior_guard', 'a guard')]))

    expect(screen.getByAltText('a guard')).toHaveAttribute('src', '/api/v1/images/mobiles/templates/warrior_guard.png')
  })

  it('links each template to its detail page', () => {
    renderWith(page([template('warrior_guard', 'a guard')]))

    expect(screen.getByRole('link', { name: 'a guard' })).toHaveAttribute('href', '/admin/mobiles/warrior_guard')
  })

  // The search is the server's. If this ever asserts filtered rows instead of the passed-through
  // term, the screen has started filtering locally and searches a single page.
  it('hands the typed search to the query', async () => {
    renderWith(page([template('warrior_guard', 'a guard')]))

    await userEvent.type(screen.getByPlaceholderText(/Search by id, name, title/), 'bri')

    expect(query.lastParams?.search).toBe('bri')
  })

  it('asks for the next page', async () => {
    renderWith(page([template('warrior_guard', 'a guard')], { totalPages: 3 }))

    await userEvent.click(screen.getByRole('button', { name: /next/i }))

    expect(query.lastParams?.page).toBe(2)
  })

  // Searching from page 3 would otherwise land on page 3 of a one-page result: an empty table that
  // reads as "no matches" for a search that in fact matched.
  it('returns to the first page when the search changes', async () => {
    renderWith(page([template('warrior_guard', 'a guard')], { totalPages: 3 }))

    await userEvent.click(screen.getByRole('button', { name: /next/i }))
    expect(query.lastParams?.page).toBe(2)

    await userEvent.type(screen.getByPlaceholderText(/Search by id, name, title/), 'd')

    expect(query.lastParams?.page).toBe(1)
  })

  it('reports the total, which counts every match and not just this page', () => {
    renderWith(page([template('warrior_guard', 'a guard')], { total: 87, totalPages: 4 }))

    expect(screen.getByText('87 templates')).toBeInTheDocument()
  })

  it('says so when a search matches nothing', () => {
    renderWith(page([], { total: 0 }))

    expect(screen.getByText('No template matches that search.')).toBeInTheDocument()
  })

  it('says so when the request failed', () => {
    renderWith(undefined, { isError: true })

    expect(screen.getByRole('alert')).toHaveTextContent('The templates could not be loaded.')
  })

  // Many spawn templates carry no name of their own — a spawn draws one from a pool — so the id is
  // the only label there is. Real data has several.
  it('falls back to the id for a template with no name', () => {
    renderWith(page([template('base_human_npc', '')]))

    expect(screen.getByRole('link', { name: 'base_human_npc' })).toBeInTheDocument()
  })
})
