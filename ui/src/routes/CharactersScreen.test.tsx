import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import i18n from '../lib/i18n'
import { CharactersScreen } from './CharactersScreen'
import type { Character } from '../lib/characters'

const query = vi.hoisted(() => ({
  data: undefined as Character[] | undefined,
  isPending: false,
  isError: false,
}))

// Only the hook is stubbed; the URL builders are the real ones, so the `src` assertions below are
// checking what the browser would actually request.
vi.mock('../lib/characters', async (original) => ({
  ...(await original<typeof import('../lib/characters')>()),
  useMyCharacters: () => query,
}))

function character(serial: string, name: string): Character {
  return {
    serial,
    name,
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
  }
}

function renderWith(data: Character[] | undefined, state: Partial<typeof query> = {}) {
  Object.assign(query, { data, isPending: false, isError: false }, state)
  return render(
    <MemoryRouter>
      <CharactersScreen />
    </MemoryRouter>,
  )
}

describe('CharactersScreen', () => {
  it('lists every character on the account', () => {
    renderWith([character('0x1', 'Squid'), character('0x2', 'Vega')])

    expect(screen.getByRole('link', { name: /Squid/ })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Vega/ })).toBeInTheDocument()
  })

  // The list is the way into the detail page, which is where a character is actually shown.
  it('links each character to its detail page', () => {
    renderWith([character('0x40000001', 'Squid')])

    expect(screen.getByRole('link', { name: /Squid/ })).toHaveAttribute('href', '/characters/0x40000001')
  })

  it('asks for the figure with the serial the API reported', () => {
    renderWith([character('0x40000001', 'Squid')])

    expect(screen.getByAltText('Squid in the world')).toHaveAttribute('src', '/api/v1/images/mobiles/0x40000001.png')
  })

  // Asserted in Italian on purpose: the API reports the race in English, so in English the
  // translated and untranslated renderings are identical and the test could not fail.
  it('translates the race', async () => {
    await i18n.changeLanguage('it')
    try {
      renderWith([{ ...character('0x1', 'Squid'), race: 'Elf' }])

      expect(screen.getByText('Elfo')).toBeInTheDocument()
    } finally {
      await i18n.changeLanguage('en')
    }
  })

  // The races are a closed enum today, but a list must not break on a value it has no word for --
  // showing the server's own is better than showing a raw translation key.
  it('shows a race it has no translation for rather than a key', async () => {
    await i18n.changeLanguage('it')
    try {
      renderWith([{ ...character('0x1', 'Squid'), race: 'Daemon' }])

      expect(screen.getByText('Daemon')).toBeInTheDocument()
    } finally {
      await i18n.changeLanguage('en')
    }
  })

  // The state a freshly registered player sees first.
  it('says so when the account has no characters', () => {
    renderWith([])

    expect(screen.getByText('No characters yet')).toBeInTheDocument()
  })

  it('says so when the request failed', () => {
    renderWith(undefined, { isError: true })

    expect(screen.getByRole('alert')).toHaveTextContent('Your characters could not be loaded.')
  })

  it('says so while loading', () => {
    renderWith(undefined, { isPending: true })

    expect(screen.getByText('Loading…')).toBeInTheDocument()
  })
})
