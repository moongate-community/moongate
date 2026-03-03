import { useThemeStore } from '../store/themeStore'

interface ThemeToggleProps {
  className?: string
}

export function ThemeToggle({ className }: ThemeToggleProps) {
  const theme = useThemeStore((s) => s.theme)
  const toggleTheme = useThemeStore((s) => s.toggleTheme)

  return (
    <button
      type="button"
      onClick={toggleTheme}
      className={className}
      style={{
        color: 'var(--mg-text)',
        background: 'var(--mg-panel-soft)',
        border: '1px solid var(--mg-border)',
        borderRadius: 'var(--mg-radius-control)',
        fontFamily: 'JetBrains Mono, monospace',
        fontSize: '11px',
        letterSpacing: '0.08em',
        textTransform: 'uppercase',
      }}
    >
      {theme === 'dark' ? '☀ Light' : '🌙 Dark'}
    </button>
  )
}
