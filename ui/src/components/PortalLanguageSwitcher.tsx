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
              borderColor: selected ? 'color-mix(in srgb, var(--mg-accent) 42%, transparent)' : 'color-mix(in srgb, var(--mg-accent) 18%, transparent)',
              color: selected ? 'var(--mg-accent)' : 'color-mix(in srgb, var(--mg-text) 72%, transparent)',
              background: selected ? 'color-mix(in srgb, var(--mg-accent) 12%, transparent)' : 'transparent',
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
