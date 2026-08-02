import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'

import '../lib/i18n'
import { ApiError } from '../lib/api'
import { CharacterDetailScreen } from './CharacterDetailScreen'
import type { CharacterDetail, CharacterItem } from '../lib/characters'

const query = vi.hoisted(() => ({
  data: undefined as CharacterDetail | undefined,
  isPending: false,
  isError: false,
  error: null as unknown,
  lastSerial: '' as string,
}))

vi.mock('../lib/characters', async (original) => ({
  ...(await original<typeof import('../lib/characters')>()),
  useCharacter: (serial: string) => {
    query.lastSerial = serial
    return query
  },
}))

function item(name: string, over: Partial<CharacterItem> = {}): CharacterItem {
  return {
    serial: '0xA',
    name,
    templateId: name,
    itemId: 0x13b9,
    hue: 0,
    amount: 1,
    layer: null,
    contents: [],
    ...over,
  } as CharacterItem
}

function detail(over: Partial<CharacterDetail> = {}): CharacterDetail {
  return {
    character: {
      serial: '0x40000001',
      name: 'Squid',
      accountUsername: 'tom',
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
    },
    equipment: [],
    backpack: [],
    skills: [],
    ...over,
  } as CharacterDetail
}

function renderAt(serial: string, data: CharacterDetail | undefined, state: Partial<typeof query> = {}) {
  Object.assign(query, { data, isPending: false, isError: false, error: null }, state)

  return render(
    <MemoryRouter initialEntries={[`/characters/${serial}`]}>
      <Routes>
        <Route path="/characters/:serial" element={<CharacterDetailScreen />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('CharacterDetailScreen', () => {
  it('asks for the character named in the route', () => {
    renderAt('0x40000001', detail())

    expect(query.lastSerial).toBe('0x40000001')
  })

  it('shows the paperdoll of that character', () => {
    renderAt('0x40000001', detail())

    expect(screen.getByAltText('Squid paperdoll')).toHaveAttribute(
      'src',
      '/api/v1/images/mobiles/0x40000001/paperdoll.png',
    )
  })

  it('shows what the character wears and what they carry', () => {
    renderAt('0x40000001', detail({ equipment: [item('Robe', { layer: 'OuterTorso' })], backpack: [item('Dagger')] }))

    expect(screen.getByText('Robe')).toBeInTheDocument()
    expect(screen.getByText('Dagger')).toBeInTheDocument()
  })

  it('shows the skills the character trained', () => {
    renderAt(
      '0x40000001',
      detail({ skills: [{ id: 1, name: 'Alchemy', value: 62.5, cap: 100, lock: 'Up' }] } as Partial<CharacterDetail>),
    )

    expect(screen.getByText('Alchemy')).toBeInTheDocument()
    expect(screen.getByText('62.5')).toBeInTheDocument()
  })

  it('shows the stats', () => {
    renderAt('0x40000001', detail())

    expect(screen.getByText('55 / 60')).toBeInTheDocument()
  })

  // "Not yours" and "does not exist" are different answers, and telling them apart is the reason
  // both messages exist.
  it('says a character belongs to someone else', () => {
    renderAt('0x2', undefined, { isError: true, error: new ApiError(403, 'forbidden') })

    expect(screen.getByRole('alert')).toHaveTextContent('That character belongs to another account.')
  })

  it('says a character does not exist', () => {
    renderAt('0xDEAD', undefined, { isError: true, error: new ApiError(404, 'not found') })

    expect(screen.getByRole('alert')).toHaveTextContent('That character could not be found.')
  })

  it('falls back to a generic failure for anything else', () => {
    renderAt('0x1', undefined, { isError: true, error: new Error('network') })

    expect(screen.getByRole('alert')).toHaveTextContent('That character could not be loaded.')
  })

  it('offers the way back to the list', () => {
    renderAt('0x40000001', detail())

    expect(screen.getByRole('link', { name: /back to characters/i })).toHaveAttribute('href', '/characters')
  })
})
