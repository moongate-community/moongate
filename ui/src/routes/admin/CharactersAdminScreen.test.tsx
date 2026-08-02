import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'

import '../../lib/i18n'
import { CharactersAdminScreen } from './CharactersAdminScreen'
import type { Character, CharacterPage } from '../../lib/characters'

const query = vi.hoisted(() => ({
  data: undefined as CharacterPage | undefined,
  isPending: false,
  isError: false,
  lastParams: null as { page: number; search: string } | null,
}))

// Only the hook is stubbed; figureUrl is the real one, so the src assertion below checks what the
// browser would actually request.
vi.mock('../../lib/characters', async (original) => ({
  ...(await original<typeof import('../../lib/characters')>()),
  useAllCharacters: (params: { page: number; search: string }) => {
    query.lastParams = params
    return query
  },
}))

function character(serial: string, name: string, account: string | null): Character {
  return {
    serial,
    name,
    accountUsername: account,
    race: 'Human',
    gender: 'Male',
    body: 400,
    strength: 60,
    dexterity: 45,
    intelligence: 30,
    hits: 55,
    hitsMax: 60,
    stamina: 45,
    staminaMax: 45,
    mana: 30,
    manaMax: 30,
    kills: 0,
    skinHue: 1002,
    hairStyle: 8252,
    hairHue: 1102,
    mapId: 1,
    x: 1495,
    y: 1629,
    z: 0,
  } as Character
}

function page(items: Character[], over: Partial<CharacterPage> = {}): CharacterPage {
  return { items, total: items.length, page: 1, pageSize: 25, totalPages: 1, ...over } as CharacterPage
}

function renderWith(data: CharacterPage | undefined, state: Partial<typeof query> = {}) {
  Object.assign(query, { data, isPending: false, isError: false }, state)
  return render(
    <MemoryRouter>
      <CharactersAdminScreen />
    </MemoryRouter>,
  )
}

describe('CharactersAdminScreen', () => {
  it('lists the characters with their owning account', () => {
    renderWith(page([character('0x1', 'Squid', 'tom'), character('0x2', 'Vega', 'alice')]))

    expect(screen.getByText('Squid')).toBeInTheDocument()
    expect(screen.getByText('alice')).toBeInTheDocument()
  })

  it('shows a preview addressed by the serial the API reported', () => {
    renderWith(page([character('0x40000001', 'Squid', 'tom')]))

    expect(screen.getByAltText('Squid')).toHaveAttribute('src', '/api/v1/images/mobiles/0x40000001.png')
  })

  // The search is the server's. If this ever asserts filtered rows instead of the passed-through
  // term, the screen has started filtering locally and searches a single page.
  it('hands the typed search to the query', async () => {
    renderWith(page([character('0x1', 'Squid', 'tom')]))

    await userEvent.type(screen.getByPlaceholderText(/Search by character/), 'veg')

    expect(query.lastParams?.search).toBe('veg')
  })

  it('asks for the next page', async () => {
    renderWith(page([character('0x1', 'Squid', 'tom')], { totalPages: 3 }))

    await userEvent.click(screen.getByRole('button', { name: /next/i }))

    expect(query.lastParams?.page).toBe(2)
  })

  // Searching from page 3 would otherwise land on page 3 of a one-page result: an empty table that
  // reads as "no matches" for a search that in fact matched.
  it('returns to the first page when the search changes', async () => {
    renderWith(page([character('0x1', 'Squid', 'tom')], { totalPages: 3 }))

    await userEvent.click(screen.getByRole('button', { name: /next/i }))
    expect(query.lastParams?.page).toBe(2)

    await userEvent.type(screen.getByPlaceholderText(/Search by character/), 'v')

    expect(query.lastParams?.page).toBe(1)
  })

  it('reports the total, which counts every match and not just this page', () => {
    renderWith(page([character('0x1', 'Squid', 'tom')], { total: 87, totalPages: 4 }))

    expect(screen.getByText('87 characters')).toBeInTheDocument()
  })

  it('says so when a search matches nothing', () => {
    renderWith(page([], { total: 0 }))

    expect(screen.getByText('No character matches that search.')).toBeInTheDocument()
  })

  it('says so when the request failed', () => {
    renderWith(undefined, { isError: true })

    expect(screen.getByRole('alert')).toHaveTextContent('The characters could not be loaded.')
  })

  it('names a character with no owning account rather than showing a blank', () => {
    renderWith(page([character('0x1', 'Squid', null)]))

    expect(screen.getByText('Unknown')).toBeInTheDocument()
  })
})
