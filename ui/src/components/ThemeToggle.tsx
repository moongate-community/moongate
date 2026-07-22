import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'

export type Theme = 'dark' | 'light'

const STORAGE_KEY = 'mg-theme'

function initialTheme(): Theme {
  const stored = localStorage.getItem(STORAGE_KEY)

  if (stored === 'dark' || stored === 'light') {
    return stored
  }

  return (document.documentElement.dataset.theme as Theme | undefined) ?? 'dark'
}

export function ThemeToggle() {
  const { t } = useTranslation()
  const [theme, setTheme] = useState<Theme>(initialTheme)

  useEffect(() => {
    document.documentElement.dataset.theme = theme
    localStorage.setItem(STORAGE_KEY, theme)
  }, [theme])

  return (
    <div className="flex gap-0.5 rounded-control border border-border-subtle bg-deep p-0.5">
      {(['dark', 'light'] as const).map((option) => (
        <button
          key={option}
          type="button"
          aria-pressed={theme === option}
          onClick={() => setTheme(option)}
          className={
            theme === option
              ? 'rounded-control bg-gold px-3 py-1 text-xs font-bold text-gold-ink'
              : 'rounded-control px-3 py-1 text-xs font-bold text-muted'
          }
        >
          {t(option === 'dark' ? 'theme.dark' : 'theme.light')}
        </button>
      ))}
    </div>
  )
}
