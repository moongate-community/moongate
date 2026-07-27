import { isAdmin } from './roles'

describe('isAdmin', () => {
  it('is true for the two staff levels', () => {
    expect(isAdmin('Administrator')).toBe(true)
    expect(isAdmin('GrandMaster')).toBe(true)
  })

  it('is false for a player, null, and anything else', () => {
    expect(isAdmin('Player')).toBe(false)
    expect(isAdmin(null)).toBe(false)
    expect(isAdmin('administrator')).toBe(false) // case-sensitive: the role names are exact
    expect(isAdmin('')).toBe(false)
  })
})
