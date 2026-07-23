import type { ReactNode } from 'react'
import { NavLink } from 'react-router'
import { useTranslation } from 'react-i18next'
import { useSession } from '../lib/auth'
import { isAdmin } from '../lib/roles'
import { ThemeToggle } from './ThemeToggle'
import icon from '../assets/moongate-icon.png'

/** The design's double bar: a 50px identity row above a 46px tab row. */
export function AppShell({ children }: { children: ReactNode }) {
  const { t } = useTranslation()
  const { username, level, signOut } = useSession()

  return (
    <div className="min-h-screen bg-page text-ink">
      <header className="flex h-[var(--mg-topbar-h)] items-center gap-3 border-b border-border-subtle bg-surface px-5">
        <img src={icon} alt="" className="size-[26px] rounded-control object-cover" />
        <span className="font-display text-[18px] font-bold tracking-wider text-gold">{t('app.name')}</span>
        <div className="flex-1" />
        <span className="text-xs tracking-widest text-faint">{t('theme.label')}</span>
        <ThemeToggle />

        {/* A rule of its own, not just a gap: theme and identity are unrelated controls, and without a
            separator the four of them read as one block. */}
        {username !== null && (
          <>
            <span className="ml-2 border-l border-border-subtle pl-4 text-sm text-ink">{username}</span>
            <button type="button" onClick={signOut} className="text-sm text-muted hover:text-gold">
              {t('common.signOut')}
            </button>
          </>
        )}
      </header>

      <nav className="flex h-[var(--mg-tabrow-h)] items-stretch gap-6 border-b border-border-subtle bg-surface px-5">
        <NavLink
          to="/"
          end
          className={({ isActive }) =>
            isActive
              ? 'flex items-center border-b-2 border-gold text-sm font-bold text-gold'
              : 'flex items-center border-b-2 border-transparent text-sm text-muted hover:text-ink'
          }
        >
          {t('nav.dashboard')}
        </NavLink>

        {isAdmin(level) && (
          <NavLink
            to="/admin"
            className={({ isActive }) =>
              isActive
                ? 'flex items-center border-b-2 border-gold text-sm font-bold text-gold'
                : 'flex items-center border-b-2 border-transparent text-sm text-muted hover:text-ink'
            }
          >
            {t('nav.admin')}
          </NavLink>
        )}
      </nav>

      <main className="mx-auto max-w-[1300px] p-6">{children}</main>
    </div>
  )
}
