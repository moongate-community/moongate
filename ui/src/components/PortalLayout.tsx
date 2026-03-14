import { useEffect, type ReactNode } from 'react'
import { PortalNavbar } from './PortalNavbar'

export function PortalLayout({ children }: { children: ReactNode }) {
  useEffect(() => {
    document.documentElement.classList.remove('light')
    document.documentElement.classList.add('dark')
    document.body.classList.add('portal-theme')

    return () => {
      document.body.classList.remove('portal-theme')
    }
  }, [])

  return (
    <div
      className="min-h-screen"
      style={{
        background: [
          'radial-gradient(circle at top, color-mix(in srgb, var(--mg-accent) 18%, transparent), transparent 22%)',
          'linear-gradient(180deg, color-mix(in srgb, var(--mg-bg) 92%, black 8%) 0%, color-mix(in srgb, var(--mg-panel) 72%, var(--mg-bg) 28%) 42%, color-mix(in srgb, var(--mg-bg) 88%, black 12%) 100%)',
        ].join(', '),
      }}
    >
      <PortalNavbar />
      <main>{children}</main>
    </div>
  )
}
