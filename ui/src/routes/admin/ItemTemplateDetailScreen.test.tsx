import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'

import '../../lib/i18n'
import { ApiError } from '../../lib/api'
import { ItemTemplateDetailScreen } from './ItemTemplateDetailScreen'
import type { ItemTemplate } from '../../lib/templates'

const query = vi.hoisted(() => ({
  data: undefined as ItemTemplate | undefined,
  isError: false,
  error: null as unknown,
  lastId: '' as string,
}))

vi.mock('../../lib/templates', async (original) => ({
  ...(await original<typeof import('../../lib/templates')>()),
  useItemTemplate: (id: string) => {
    query.lastId = id
    return query
  },
}))

function template(over: Partial<ItemTemplate> = {}): ItemTemplate {
  return {
    id: 'plate_mail',
    name: 'Plate Mail',
    category: 'armor',
    description: 'Heavy plate armour.',
    itemId: 0x1415,
    hue: 0,
    rarity: 'Rare',
    goldValue: 250,
    weight: 40,
    scriptId: undefined,
    isMovable: true,
    stackable: false,
    dyeable: true,
    visibility: 'Visible',
    lootType: 'Regular',
    tags: ['armor'],
    flippableItemIds: [],
    lootTables: [],
    params: {},
    equip: undefined,
    weapon: undefined,
    container: undefined,
    book: undefined,
    imageUrl: '/api/v1/images/items/0x1415.png',
    ...over,
  } as ItemTemplate
}

function renderAt(id: string, data: ItemTemplate | undefined, state: Partial<typeof query> = {}) {
  Object.assign(query, { data, isError: false, error: null }, state)

  return render(
    <MemoryRouter initialEntries={[`/admin/items/${id}`]}>
      <Routes>
        <Route path="/admin/items/:id" element={<ItemTemplateDetailScreen />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('ItemTemplateDetailScreen', () => {
  it('asks for the template named in the route', () => {
    renderAt('plate_mail', template())

    expect(query.lastId).toBe('plate_mail')
  })

  it('shows the name, the description and the sprite', () => {
    renderAt('plate_mail', template())

    expect(screen.getByText('Plate Mail')).toBeInTheDocument()
    expect(screen.getByText('Heavy plate armour.')).toBeInTheDocument()
    expect(screen.getByAltText('Plate Mail')).toHaveAttribute('src', '/api/v1/images/items/0x1415.png')
  })

  it('shows the flags in words', () => {
    renderAt('plate_mail', template({ isMovable: true, stackable: false }))

    expect(screen.getByText('Movable').nextSibling).toHaveTextContent('Yes')
    expect(screen.getByText('Stackable').nextSibling).toHaveTextContent('No')
  })

  // Specs are nullable, and an empty card for a template that has none is noise.
  it('omits a spec the template does not have', () => {
    renderAt('plate_mail', template({ weapon: undefined }))

    expect(screen.queryByText('Weapon')).not.toBeInTheDocument()
  })

  it('shows a spec the template does have', () => {
    // `layer` is a numeric enum on the equip spec, unlike the string layer a worn item reports.
    renderAt('plate_mail', template({ equip: { layer: 13 } }))

    expect(screen.getByText('Equip')).toBeInTheDocument()
  })

  // "No such template" and "the request broke" are different answers, and an id typed by hand makes
  // the first one common.
  it('says so when there is no template with that id', () => {
    renderAt('nope', undefined, { isError: true, error: new ApiError(404, 'not found') })

    expect(screen.getByRole('alert')).toHaveTextContent('No template with that id.')
  })

  it('falls back to a generic failure for anything else', () => {
    renderAt('plate_mail', undefined, { isError: true, error: new Error('network') })

    expect(screen.getByRole('alert')).toHaveTextContent('The templates could not be loaded.')
  })

  it('offers the way back to the catalogue', () => {
    renderAt('plate_mail', template())

    expect(screen.getByRole('link', { name: /back to templates/i })).toHaveAttribute('href', '/admin/items')
  })
})
