import type { PortalLocale } from './portal/messages'
import { portalDefaultLocale } from './portal/messages'

export const portalLocaleStorageKey = 'moongate.portal.locale'

export function isPortalLocale(value: string | null | undefined): value is PortalLocale {
  return value === 'en' || value === 'it'
}

export function resolvePortalLocale(): PortalLocale {
  const persisted = typeof window !== 'undefined' ? window.localStorage.getItem(portalLocaleStorageKey) : null
  if (isPortalLocale(persisted)) {
    return persisted
  }

  const browserValue = typeof navigator !== 'undefined' ? navigator.language.toLowerCase() : ''
  if (browserValue.startsWith('it')) {
    return 'it'
  }

  if (browserValue.startsWith('en')) {
    return 'en'
  }

  return portalDefaultLocale
}

export function persistPortalLocale(locale: PortalLocale): void {
  if (typeof window === 'undefined') {
    return
  }

  window.localStorage.setItem(portalLocaleStorageKey, locale)
}
