import { describe, expect, it } from 'vitest'

import { charactersQuery, figureUrl, itemImageUrl, paperdollUrl } from './characters'

// The serial is used verbatim, in the `0x40000001` form the API reports it in. The routes accept
// that form, so nothing here converts bases -- a conversion is the kind of thing that silently
// drifts from what the server accepts.
describe('character image urls', () => {
  it('addresses a character by the serial the API reported', () => {
    expect(figureUrl('0x40000001')).toBe('/api/v1/images/mobiles/0x40000001.png')
  })

  it('asks for the paperdoll with its backdrop by default', () => {
    expect(paperdollUrl('0x40000001')).toBe('/api/v1/images/mobiles/0x40000001/paperdoll.png')
  })

  it('can drop the backdrop', () => {
    expect(paperdollUrl('0x40000001', false)).toBe('/api/v1/images/mobiles/0x40000001/paperdoll.png?background=false')
  })

  // The ETag is a fingerprint of the appearance, so the browser revalidates and takes a 304 until
  // the character changes clothes. A cache-buster would force a full download on every visit --
  // it is the natural reflex when a picture might look stale, and here it is exactly wrong.
  it('never adds a cache-busting parameter', () => {
    expect(figureUrl('0x1')).not.toMatch(/[?&](t|v|_)=/)
    expect(paperdollUrl('0x1')).not.toMatch(/[?&](t|v|_)=/)
  })
})

describe('staff characters query', () => {
  it('asks for the page it was given', () => {
    expect(charactersQuery({ page: 3, search: '' })).toBe('/api/v1/admin/characters?page=3&pageSize=25')
  })

  // Blank means no filter; sending `search=` would be a filter on the empty string.
  it('omits an empty search', () => {
    expect(charactersQuery({ page: 1, search: '   ' })).toBe('/api/v1/admin/characters?page=1&pageSize=25')
  })

  it('encodes the search', () => {
    expect(charactersQuery({ page: 1, search: 'lord blackthorn' })).toBe(
      '/api/v1/admin/characters?page=1&pageSize=25&search=lord+blackthorn',
    )
  })

  // page 0 is a 400 from the server, so a bug that reaches it should be visible here rather than as
  // a failed request.
  it('never asks for a page below 1', () => {
    expect(charactersQuery({ page: 0, search: '' })).toBe('/api/v1/admin/characters?page=1&pageSize=25')
  })
})

describe('item art urls', () => {
  // The route takes the ART id in hex, with or without the prefix -- not the item's serial.
  it('addresses the art by its hex id', () => {
    expect(itemImageUrl(0x1234)).toBe('/api/v1/images/items/0x1234.png')
  })

  it('asks for the hue when the item is dyed', () => {
    expect(itemImageUrl(0x1234, 0x21)).toBe('/api/v1/images/items/0x1234.png?hue=0x21')
  })

  // Hue 0 IS the raw art, so sending it is noise that keeps two spellings of one picture in the cache.
  it('omits hue 0', () => {
    expect(itemImageUrl(0x1234, 0)).toBe('/api/v1/images/items/0x1234.png')
  })

  it('never adds a cache-busting parameter', () => {
    expect(itemImageUrl(0x1234, 0x21)).not.toMatch(/[?&](t|v|_)=/)
  })
})
