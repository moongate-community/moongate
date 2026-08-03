import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'

import '../../lib/i18n'
import { ApiError } from '../../lib/api'
import { MobileTemplateDetailScreen } from './MobileTemplateDetailScreen'
import type { MobileTemplate } from '../../lib/templates'

const query = vi.hoisted(() => ({
  data: undefined as MobileTemplate | undefined,
  isError: false,
  error: null as unknown,
  lastId: '' as string,
}))

vi.mock('../../lib/templates', async (original) => ({
  ...(await original<typeof import('../../lib/templates')>()),
  useMobileTemplate: (id: string) => {
    query.lastId = id
    return query
  },
}))

function template(over: Partial<MobileTemplate> = {}): MobileTemplate {
  return {
    id: 'warrior_guard_npc',
    name: 'a guard',
    namePool: 'guards',
    gender: 'Male',
    title: 'the guard',
    category: 'npc',
    description: 'A town guard.',
    tags: ['npc', 'guard'],
    baseMobile: 'base_human_npc',
    strength: 96,
    dexterity: 70,
    intelligence: 30,
    skills: { Swordsmanship: 80 },
    appearance: {
      body: 400,
      skinHue: 'hue(1002:1058)',
      hairStyle: 8251,
      hairHue: 'hue(1102:1149)',
      facialHairStyle: 0,
      facialHairHue: undefined,
    },
    equipment: [{ item: 'plate_chest', layer: 'InnerTorso', hue: undefined }],
    variants: [
      {
        name: 'female',
        weight: 1,
        gender: 'Female',
        namePool: undefined,
        lootTableId: undefined,
        appearance: {
          body: 401,
          skinHue: undefined,
          hairStyle: 0,
          hairHue: undefined,
          facialHairStyle: 0,
          facialHairHue: undefined,
        },
        equipment: [],
      },
    ],
    lootTableId: 'guard_loot',
    brainScript: undefined,
    imageUrl: '/api/v1/images/mobiles/templates/warrior_guard_npc.png',
    paperdollUrl: '/api/v1/images/mobiles/templates/warrior_guard_npc/paperdoll.png',
    ...over,
  } as MobileTemplate
}

function renderAt(id: string, data: MobileTemplate | undefined, state: Partial<typeof query> = {}) {
  Object.assign(query, { data, isError: false, error: null }, state)

  return render(
    <MemoryRouter initialEntries={[`/admin/mobiles/${id}`]}>
      <Routes>
        <Route path="/admin/mobiles/:id" element={<MobileTemplateDetailScreen />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('MobileTemplateDetailScreen', () => {
  it('asks for the template named in the route', () => {
    renderAt('warrior_guard_npc', template())

    expect(query.lastId).toBe('warrior_guard_npc')
  })

  it('shows both pictures', () => {
    renderAt('warrior_guard_npc', template())

    const pictures = screen.getAllByAltText('a guard')

    expect(pictures.map((picture) => picture.getAttribute('src'))).toEqual([
      '/api/v1/images/mobiles/templates/warrior_guard_npc.png',
      '/api/v1/images/mobiles/templates/warrior_guard_npc/paperdoll.png',
    ])
  })

  it('shows the skills and the equipment', () => {
    renderAt('warrior_guard_npc', template())

    expect(screen.getByText('Swordsmanship')).toBeInTheDocument()
    expect(screen.getByText('plate_chest')).toBeInTheDocument()
  })

  it('shows each variant with its own body', () => {
    renderAt('warrior_guard_npc', template())

    expect(screen.getByText('female')).toBeInTheDocument()
    expect(screen.getByText('401')).toBeInTheDocument()
  })

  // Hues are specs — a range resolved per spawn — and must survive to the screen intact.
  it('shows a hue spec verbatim', () => {
    renderAt('warrior_guard_npc', template())

    expect(screen.getByText('hue(1002:1058)')).toBeInTheDocument()
  })

  // Null gender means the template lets a spawn pick, which is not the same as male.
  it('says a template fixes no gender', () => {
    renderAt('base_human_npc', template({ gender: undefined }))

    expect(screen.getAllByText('Any').length).toBeGreaterThan(0)
  })

  // Many spawn templates carry no name of their own: a spawn draws one from a pool.
  it('falls back to the id when the template has no name', () => {
    renderAt('base_human_npc', template({ id: 'base_human_npc', name: '' }))

    expect(screen.getByRole('heading', { name: 'base_human_npc' })).toBeInTheDocument()
  })

  it('says so when there is no template with that id', () => {
    renderAt('nope', undefined, { isError: true, error: new ApiError(404, 'not found') })

    expect(screen.getByRole('alert')).toHaveTextContent('No template with that id.')
  })

  it('offers the way back to the catalogue', () => {
    renderAt('warrior_guard_npc', template())

    expect(screen.getByRole('link', { name: /back to templates/i })).toHaveAttribute('href', '/admin/mobiles')
  })
})
