import type { ReactNode } from 'react'
import { PortalNavbar } from './PortalNavbar'

export function PortalLayout({ children }: { children: ReactNode }) {
  return (
    <div
      className="min-h-screen"
      style={{
        background: [
          'radial-gradient(circle at top, rgba(214,179,106,0.07), transparent 22%)',
          'linear-gradient(180deg, #19140f 0%, #241b14 42%, #16110d 100%)',
        ].join(', '),
      }}
    >
      <PortalNavbar />
      <main>{children}</main>
    </div>
  )
}
