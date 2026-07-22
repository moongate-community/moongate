import { applyStoredTheme, applyTheme, readTheme } from './theme'

describe('theme', () => {
  beforeEach(() => {
    localStorage.clear()
    delete document.documentElement.dataset.theme
  })

  it('remembers a choice and puts it on the document', () => {
    applyTheme('light')

    expect(document.documentElement.dataset.theme).toBe('light')
    expect(localStorage.getItem('mg-theme')).toBe('light')
  })

  // The screen a visitor meets first has no theme toggle on it, so nothing there would otherwise read
  // the remembered choice: the portal would open in the default theme however often you changed it.
  it('applies the remembered theme with no toggle mounted', () => {
    localStorage.setItem('mg-theme', 'light')

    applyStoredTheme()

    expect(document.documentElement.dataset.theme).toBe('light')
  })

  it('falls back to dark when nothing is remembered', () => {
    applyStoredTheme()

    expect(readTheme()).toBe('dark')
    expect(document.documentElement.dataset.theme).toBe('dark')
  })

  it('ignores a stored value that is not a theme', () => {
    localStorage.setItem('mg-theme', 'chartreuse')

    applyStoredTheme()

    expect(document.documentElement.dataset.theme).toBe('dark')
  })
})
