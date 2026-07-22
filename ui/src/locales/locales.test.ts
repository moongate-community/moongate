import english from './en.json'

// Not `it`: that name is vitest's own test function, and importing over it makes every case in this
// file fail to parse.
import italian from './it.json'

/** Every leaf key, dotted — `login.title` rather than a nested object. */
function keysOf(value: Record<string, unknown>, prefix = ''): string[] {
  return Object.entries(value).flatMap(([key, child]) =>
    typeof child === 'object' && child !== null
      ? keysOf(child as Record<string, unknown>, `${prefix}${key}.`)
      : [`${prefix}${key}`],
  )
}

describe('locales', () => {
  // A key added to one file and forgotten in the other does not fail anything at runtime: i18next falls
  // back to English and the gap shows up as an English word in an Italian sentence, or as the raw key.
  // Cheap to guard, and invisible otherwise until someone reads the screen in the other language.
  it('define exactly the same keys', () => {
    expect(keysOf(italian).sort()).toEqual(keysOf(english).sort())
  })

  it('leave no value empty', () => {
    for (const [name, bundle] of [
      ['en', english],
      ['it', italian],
    ] as const) {
      const blank = keysOf(bundle).filter((key) => {
        const value = key.split('.').reduce<unknown>((node, part) => (node as never)[part], bundle)
        return typeof value !== 'string' || value.trim() === ''
      })

      expect(blank, `${name} has blank values`).toEqual([])
    }
  })
})
