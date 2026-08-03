import { describe, expect, it } from 'vitest'

import { itemTemplatesQuery, mobileTemplatesQuery } from './templates'

describe('item template query', () => {
  it('asks for the page it was given', () => {
    expect(itemTemplatesQuery({ page: 3, search: '' })).toBe('/api/v1/admin/items/templates?page=3&pageSize=25')
  })

  // Blank means no filter; sending `search=` would filter on the empty string.
  it('omits an empty search', () => {
    expect(itemTemplatesQuery({ page: 1, search: '  ' })).toBe('/api/v1/admin/items/templates?page=1&pageSize=25')
  })

  it('encodes the search', () => {
    expect(itemTemplatesQuery({ page: 1, search: 'plate mail' })).toBe(
      '/api/v1/admin/items/templates?page=1&pageSize=25&search=plate+mail',
    )
  })

  // page 0 is a 400 from the server, so a bug that reaches it should be visible here rather than as
  // a failed request.
  it('never asks for a page below 1', () => {
    expect(itemTemplatesQuery({ page: 0, search: '' })).toBe('/api/v1/admin/items/templates?page=1&pageSize=25')
  })
})

describe('mobile template query', () => {
  it('asks for the page it was given', () => {
    expect(mobileTemplatesQuery({ page: 2, search: '' })).toBe('/api/v1/admin/mobiles/templates?page=2&pageSize=25')
  })

  it('omits an empty search', () => {
    expect(mobileTemplatesQuery({ page: 1, search: ' ' })).toBe('/api/v1/admin/mobiles/templates?page=1&pageSize=25')
  })

  it('encodes the search', () => {
    expect(mobileTemplatesQuery({ page: 1, search: 'town guard' })).toBe(
      '/api/v1/admin/mobiles/templates?page=1&pageSize=25&search=town+guard',
    )
  })

  it('never asks for a page below 1', () => {
    expect(mobileTemplatesQuery({ page: 0, search: '' })).toBe('/api/v1/admin/mobiles/templates?page=1&pageSize=25')
  })
})
