import en from './en'
import it from './it'

export type PortalLocale = 'en' | 'it'

export const portalDefaultLocale: PortalLocale = 'en'

export const portalMessages: Record<PortalLocale, Record<string, string>> = {
  en,
  it,
}
