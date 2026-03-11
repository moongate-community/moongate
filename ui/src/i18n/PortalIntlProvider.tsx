import { useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { IntlProvider } from 'react-intl'
import { persistPortalLocale, resolvePortalLocale } from './portalLocale'
import type { PortalLocale } from './portal/messages'
import { portalDefaultLocale, portalMessages } from './portal/messages'
import { PortalIntlContext } from './PortalIntlContext'
import type { PortalIntlContextValue } from './PortalIntlContext'

export function PortalIntlProvider({ children }: { children: ReactNode }) {
  const [locale, setLocaleState] = useState<PortalLocale>(() => resolvePortalLocale())

  const value = useMemo<PortalIntlContextValue>(
    () => ({
      locale,
      setLocale(nextLocale) {
        setLocaleState(nextLocale)
        persistPortalLocale(nextLocale)
      },
    }),
    [locale],
  )

  return (
    <PortalIntlContext.Provider value={value}>
      <IntlProvider
        locale={locale}
        defaultLocale={portalDefaultLocale}
        messages={portalMessages[locale]}
      >
        {children}
      </IntlProvider>
    </PortalIntlContext.Provider>
  )
}
