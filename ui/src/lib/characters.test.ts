import { describe, expect, it } from 'vitest'

import { figureUrl, paperdollUrl } from './characters'

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
