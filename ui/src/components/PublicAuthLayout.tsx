import type { PropsWithChildren } from 'react'
import { Navigate } from 'react-router'
import { useTranslation } from 'react-i18next'
import { useSession } from '../lib/auth'
import { useServerInfo, useStats, useVersion } from '../lib/queries'
import heroArt from '../assets/login-hero.jpg'

export function PublicAuthLayout({ children }: PropsWithChildren) {
  const { t } = useTranslation()
  const { status } = useSession()
  const stats = useStats()
  const version = useVersion()
  const serverInfo = useServerInfo()

  // The assets map is keyed in PascalCase ("Logo") while the slot is lowercase — match case-insensitively.
  const logoUrl = Object.entries(serverInfo.data?.assets ?? {}).find(([key]) => key.toLowerCase() === 'logo')?.[1]

  if (status === 'authenticated') {
    return <Navigate to="/" replace />
  }

  return (
    <div className="flex min-h-screen bg-surface">
      {/* Full-bleed hero art, cropped to fill the tall panel rather than letterboxed inside it. */}
      <div
        className="relative hidden flex-[1.3] flex-col justify-end border-r border-border-subtle bg-cover bg-center p-10 lg:flex"
        style={{ backgroundImage: `url(${heroArt})` }}
      >
        {/* A bottom-up scrim: the art is bright and busy, so the quote and count need a dark ground under
            them to stay legible. */}
        <div className="pointer-events-none absolute inset-0 bg-gradient-to-t from-black/85 via-black/35 to-transparent" />

        {/* The shard's own tagline when it has set one, otherwise the bundled default. */}
        <p className="relative font-display text-[18px] text-gold">
          &ldquo;{serverInfo.data?.tagline || t('login.quote')}&rdquo;
        </p>

        {/* Real, and public: /api/v1/stats is anonymous, so the count is readable before anyone signs in.
            Rendered only once the reply lands — a zero here means an empty shard, which is worth saying,
            but saying it before asking would be a guess. */}
        {stats.data !== undefined && (
          <p className="relative mt-1 text-sm text-muted">{t('login.online', { count: stats.data.players.online })}</p>
        )}
      </div>

      <div className="flex w-full max-w-[420px] flex-col justify-center gap-4 px-12">
        {/* The shard's own logo, if it has uploaded one — public and anonymous, so it brands the page
            before anyone signs in. Absent by default, so a fresh shard just shows the wordmark title. */}
        {logoUrl !== undefined && (
          <img src={logoUrl} alt={t('login.logoAlt')} className="mb-1 max-h-16 max-w-[220px] object-contain" />
        )}

        {children}

        {/* Data from the anonymous /api/v1/version, not UI copy — no i18n key needed. Rendered only once
            it resolves, so an unreached shard shows nothing rather than "undefined". */}
        {version.data?.version !== undefined && (
          <p className="text-xs text-faint">
            {version.data.shardName} · v{version.data.version}
          </p>
        )}
      </div>
    </div>
  )
}
