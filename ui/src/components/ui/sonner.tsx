import { Toaster as Sonner, toast } from 'sonner'
import type { CSSProperties } from 'react'

// A single Toaster mounted at the app root; sonner keeps the portal, stacking and duration bar. Its
// colours come from its own CSS variables, which default to a light palette — so they are pointed at the
// Moongate tokens here. Because the tokens themselves flip with `data-theme`, the toast follows the theme
// with no `theme` prop and no `dark:`.
const surfaceVars = {
  '--normal-bg': 'var(--mg-surface)',
  '--normal-text': 'var(--mg-text)',
  '--normal-border': 'var(--mg-border)',
  '--border-radius': 'var(--mg-radius-card)',
} as CSSProperties

export function Toaster() {
  return (
    <Sonner
      position="bottom-right"
      style={surfaceVars}
      toastOptions={{
        classNames: {
          description: 'text-muted',
          success: 'border-success/40',
          error: 'border-danger/40',
          warning: 'border-gold/40',
          info: 'border-info/40',
        },
      }}
    />
  )
}

export { toast }
