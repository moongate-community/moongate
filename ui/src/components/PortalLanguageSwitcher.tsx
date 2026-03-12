import { ButtonGroup, Button } from '@heroui/react'
import { useIntl } from 'react-intl'
import { usePortalIntlContext } from '../i18n/usePortalIntlContext'
import type { PortalLocale } from '../i18n/portal/messages'

const locales: PortalLocale[] = ['en', 'it']

export function PortalLanguageSwitcher() {
  const intl = useIntl()
  const { locale, setLocale } = usePortalIntlContext()

  return (
    <ButtonGroup size="sm" variant="bordered">
      {locales.map((entry) => {
        const selected = locale === entry

        return (
          <Button
            key={entry}
            className="font-mono text-[11px] uppercase tracking-[0.18em]"
            style={{
              minWidth: '58px',
              borderColor: selected ? 'rgba(196,154,94,0.42)' : 'rgba(196,154,94,0.18)',
              color: selected ? '#f4d6a0' : 'rgba(249,244,237,0.72)',
              background: selected ? 'rgba(196,154,94,0.12)' : 'transparent',
            }}
            onPress={() => setLocale(entry)}
          >
            {intl.formatMessage({ id: `portal.language.${entry}` })}
          </Button>
        )
      })}
    </ButtonGroup>
  )
}
