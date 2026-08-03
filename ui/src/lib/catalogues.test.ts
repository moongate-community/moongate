import { CATALOGUES, cataloguePath, CATALOGUE_PAGE_SIZE } from './catalogues'

describe('cataloguePath', () => {
  it('asks for the page and the size the pager draws', () => {
    expect(cataloguePath('/api/v1/admin/hues', { page: 3, search: '' })).toBe(
      `/api/v1/admin/hues?page=3&pageSize=${CATALOGUE_PAGE_SIZE}`,
    )
  })

  // A blank search sent as an empty parameter means the same to the server but a different query key
  // here, so paging with an empty box would miss the cache every time.
  it('leaves a blank search out entirely', () => {
    expect(cataloguePath('/api/v1/admin/hues', { page: 1, search: '   ' })).not.toContain('search')
  })

  it('trims a search it does send', () => {
    expect(cataloguePath('/api/v1/admin/hues', { page: 1, search: '  red  ' })).toContain('search=red')
  })

  it('leaves a blank filter out and sends one that is set', () => {
    const none = cataloguePath('/api/v1/admin/uo-items', { page: 1, search: '', filter: { param: 'flag', value: '' } })
    const set = cataloguePath('/api/v1/admin/uo-items', {
      page: 1,
      search: '',
      filter: { param: 'flag', value: 'Container' },
    })

    expect(none).not.toContain('flag')
    expect(set).toContain('flag=Container')
  })

  it('never asks for a page before the first', () => {
    expect(cataloguePath('/api/v1/admin/hues', { page: 0, search: '' })).toContain('page=1')
  })
})

describe('CATALOGUES', () => {
  it('covers the six catalogues the shard exposes', () => {
    expect(CATALOGUES.map((catalogue) => catalogue.id)).toEqual([
      'itemTemplates',
      'mobileTemplates',
      'uoItems',
      'hues',
      'bodies',
      'hairStyles',
    ])
  })

  it('gives every catalogue a distinct path', () => {
    const paths = CATALOGUES.map((catalogue) => catalogue.path)

    expect(new Set(paths).size).toBe(paths.length)
  })

  it('draws a hue as a colour rather than as art', () => {
    const hues = CATALOGUES.find((catalogue) => catalogue.id === 'hues')!
    const entry = hues.toEntry({ value: 1002, name: 'blood red', hex: '#8B0000', gradient: [] } as never)

    expect(entry).toMatchObject({ value: '1002', label: 'blood red', detail: '1002', swatch: '#8B0000' })
    expect(entry.imageUrl).toBeUndefined()
  })

  // Unused tiledata ids have no name, and a row with an empty first line reads as broken.
  it('labels a nameless tile by its hex', () => {
    const items = CATALOGUES.find((catalogue) => catalogue.id === 'uoItems')!
    const entry = items.toEntry({ itemId: 9, hex: '0x0009', name: '', flags: [], imageUrl: '/art.png' } as never)

    expect(entry.label).toBe('0x0009')
  })

  it('offers a flag filter on the raw tiles and nowhere else', () => {
    const withFilter = CATALOGUES.filter((catalogue) => catalogue.filter !== undefined)

    expect(withFilter.map((catalogue) => catalogue.id)).toEqual(['uoItems'])
    expect(withFilter[0].filter!.values[0]).toBe('')
  })
})
