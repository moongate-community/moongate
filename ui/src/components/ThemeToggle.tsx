import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Moon, Sun } from 'lucide-react'
import { applyTheme, readTheme, type Theme } from '../lib/theme'

// A single icon button: the moon while dark is active, the sun while light is; clicking flips to the
// other. The aria-label names the switch it performs, so it stays operable and testable without the
// old two-button labels.
export function ThemeToggle() {
  const { t } = useTranslation()
  const [theme, setTheme] = useState<Theme>(readTheme)

  useEffect(() => {
    applyTheme(theme)
  }, [theme])

  const target: Theme = theme === 'dark' ? 'light' : 'dark'
  const Icon = theme === 'dark' ? Moon : Sun

  return (
    <button
      type="button"
      aria-label={t('theme.switchTo', { theme: t(`theme.${target}`) })}
      onClick={() => setTheme(target)}
      className="rounded-control border border-border-subtle bg-deep p-1.5 text-muted hover:text-gold"
    >
      <Icon className="size-4" />
    </button>
  )
}
