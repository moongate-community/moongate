export type Theme = 'dark' | 'light'

const STORAGE_KEY = 'mg-theme'

/** The remembered choice, or the dark the design ships as its default. */
export function readTheme(): Theme {
  const stored = localStorage.getItem(STORAGE_KEY)

  return stored === 'dark' || stored === 'light' ? stored : 'dark'
}

/** Switches the whole interface: every colour is a custom property keyed off this attribute. */
export function applyTheme(theme: Theme): void {
  document.documentElement.dataset.theme = theme
  localStorage.setItem(STORAGE_KEY, theme)
}

/**
 * Puts the remembered theme on the document at start-up, without recording anything.
 *
 * Called from the entry point rather than from the toggle, because the toggle lives in the app shell
 * and the login screen has no shell: leaving it to the toggle means the first screen a visitor meets
 * always renders in the default theme, however many times they chose the other one.
 */
export function applyStoredTheme(): void {
  document.documentElement.dataset.theme = readTheme()
}
