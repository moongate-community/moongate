import { createContext } from 'react'
import type { PortalLocale } from './portal/messages'

export interface PortalIntlContextValue {
  locale: PortalLocale
  setLocale: (locale: PortalLocale) => void
}

export const PortalIntlContext = createContext<PortalIntlContextValue | null>(null)
