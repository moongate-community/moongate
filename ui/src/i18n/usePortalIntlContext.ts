import { useContext } from 'react'
import { PortalIntlContext } from './PortalIntlContext'
import type { PortalIntlContextValue } from './PortalIntlContext'

export function usePortalIntlContext(): PortalIntlContextValue {
  const context = useContext(PortalIntlContext)

  if (!context) {
    throw new Error('usePortalIntlContext must be used within PortalIntlProvider')
  }

  return context
}
