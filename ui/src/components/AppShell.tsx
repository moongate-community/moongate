import type { ReactNode } from 'react'
import { NavLink } from 'react-router'
import { useTranslation } from 'react-i18next'
import { useSession } from '../lib/auth'
import { isAdmin } from '../lib/roles'
import { useStats, useVersion } from '../lib/queries'
import { Badge } from './ui/badge'
import { ThemeToggle } from './ThemeToggle'
import icon from '../assets/moongate-icon.png'

/** The design's double bar: a 50px identity row above a 46px tab row. */
export function AppShell({ children }: { children: ReactNode }) {
  const { t } = useTranslation()
  const { username, level, signOut } = useSession()
  const version = useVersion()
  const stats = useStats()
  const staff = isAdmin(level)

  return (
    <div className="min-h-screen bg-page text-ink">
      <header className="flex h-[var(--mg-topbar-h)] items-center gap-3 border-b border-border-subtle bg-surface px-5">
        <img src={icon} alt="" className="size-[26px] rounded-control object-cover" />
        <span className="font-display text-[18px] font-bold tracking-wider text-gold">{t('app.name')}</span>

        {/* The shard's own name and build, from /version. The design draws a shard picker here; there
            is one shard, so this states it rather than pretending you could switch. */}
        {version.data !== undefined && (
          <span className="flex items-center gap-2 rounded-control border border-border-subtle bg-deep px-[10px] py-[5px] text-[12.5px] text-ink">
            {version.data.shardName}
            <span className="font-mono text-[11px] text-faint">v{version.data.version}</span>
          </span>
        )}

        {/* Green because the shard answered just now, not because a badge is nice to have. */}
        {stats.isSuccess && <Badge variant="success">{t('app.live')}</Badge>}

        <div className="flex-1" />

        {/* The live number the design puts here for players is their gold; the shard's equivalent is
            how many people are in the world, and unlike gold it exists. */}
        {stats.data !== undefined && (
          <span className="font-mono text-[13px] font-bold text-gold">
            {t('app.online', { count: stats.data.players.online })}
          </span>
        )}

        <ThemeToggle />

        {/* A rule of its own, not just a gap: theme and identity are unrelated controls, and without a
            separator the four of them read as one block. */}
        {username !== null && (
          <>
            <span className="ml-2 border-l border-border-subtle pl-4">
              {staff ? (
                <Badge variant="staff">{`${level} · ${username}`}</Badge>
              ) : (
                <span className="text-sm text-ink">{username}</span>
              )}
            </span>
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

        <NavLink
          to="/characters"
          className={({ isActive }) =>
            isActive
              ? 'flex items-center border-b-2 border-gold text-sm font-bold text-gold'
              : 'flex items-center border-b-2 border-transparent text-sm text-muted hover:text-ink'
          }
        >
          {t('nav.characters')}
        </NavLink>

        <NavLink
          to="/map"
          className={({ isActive }) =>
            isActive
              ? 'flex items-center border-b-2 border-gold text-sm font-bold text-gold'
              : 'flex items-center border-b-2 border-transparent text-sm text-muted hover:text-ink'
          }
        >
          {t('nav.map')}
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
