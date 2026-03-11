import { useEffect } from 'react'
import { applyThemeToDocument, useThemeStore } from '../store/themeStore'

export function ThemeSync() {
  const theme = useThemeStore((s) => s.theme)

  useEffect(() => {
    applyThemeToDocument(theme)
  }, [theme])

  return null
}
