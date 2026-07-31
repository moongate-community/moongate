import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { fireEvent } from '@testing-library/dom'
import '../lib/i18n'
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
  return render(<CharactersScreen />)
}

describe('CharactersScreen', () => {
  it('lists every character on the account', () => {
    renderWith([character('0x1', 'Squid'), character('0x2', 'Vega')])

    expect(screen.getByRole('button', { name: /Squid/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Vega/ })).toBeInTheDocument()
  })

  // The panel is never empty while the account has characters, so arriving shows one.
  it('shows the first character on arrival', () => {
    renderWith([character('0x1', 'Squid'), character('0x2', 'Vega')])

    expect(screen.getByAltText('Squid paperdoll')).toHaveAttribute('src', '/api/v1/images/mobiles/0x1/paperdoll.png')
  })

  it('shows the character that was chosen', async () => {
    renderWith([character('0x1', 'Squid'), character('0x2', 'Vega')])

    await userEvent.click(screen.getByRole('button', { name: /Vega/ }))

    expect(screen.getByAltText('Vega paperdoll')).toHaveAttribute('src', '/api/v1/images/mobiles/0x2/paperdoll.png')
  })

  it('asks for the figure with the serial the API reported', () => {
    renderWith([character('0x40000001', 'Squid')])

    expect(screen.getByAltText('Squid in the world')).toHaveAttribute('src', '/api/v1/images/mobiles/0x40000001.png')
  })

  it('shows the pools against their maximum', () => {
    renderWith([character('0x1', 'Squid')])

    expect(screen.getByText('55 / 60')).toBeInTheDocument()
  })

  // A body with no animation legitimately 404s from the image route, and a broken-image icon is
  // not an answer.
  it('replaces a picture that fails to load', () => {
    renderWith([character('0x1', 'Squid')])

    fireEvent.error(screen.getByAltText('Squid paperdoll'))

    expect(screen.queryByAltText('Squid paperdoll')).not.toBeInTheDocument()
    expect(screen.getByText('No picture for this character')).toBeInTheDocument()
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
