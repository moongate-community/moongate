import { useState, type FormEvent } from 'react'
import { Navigate } from 'react-router'
import { useTranslation } from 'react-i18next'
import { ApiError } from '../lib/api'
import { useSession } from '../lib/auth'
import { useServerInfo, useStats, useVersion } from '../lib/queries'
import { Button } from '../components/ui/button'
import { Input } from '../components/ui/input'
import { Label } from '../components/ui/label'
import heroArt from '../assets/login-hero.jpg'

export function LoginScreen() {
  const { t } = useTranslation()
  const { status, signIn } = useSession()
  const stats = useStats()
  const version = useVersion()
  const serverInfo = useServerInfo()

  // The assets map is keyed in PascalCase ("Logo") while the slot is lowercase — match case-insensitively.
  const logoUrl = Object.entries(serverInfo.data?.assets ?? {}).find(([key]) => key.toLowerCase() === 'logo')?.[1]
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [remember, setRemember] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setBusy(true)

    try {
      await signIn(username, password, remember)
    } catch (caught) {
      // 401 is the one failure worth naming. Anything else — the shard is down, the reply was not JSON —
      // says nothing useful about the credentials, and guessing would send people to reset a password
      // that was never wrong.
      setError(caught instanceof ApiError && caught.status === 401 ? t('login.failed') : t('error.generic'))
    } finally {
      setBusy(false)
    }
  }

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

        <p className="relative font-display text-[18px] text-gold">&ldquo;{t('login.quote')}&rdquo;</p>

        {/* Real, and public: /api/v1/stats is anonymous, so the count is readable before anyone signs in.
            Rendered only once the reply lands — a zero here means an empty shard, which is worth saying,
            but saying it before asking would be a guess. */}
        {stats.data !== undefined && (
          <p className="relative mt-1 text-sm text-muted">
            {t('login.online', { count: stats.data.players.online })}
          </p>
        )}
      </div>

      <form onSubmit={submit} className="flex w-full max-w-[420px] flex-col justify-center gap-4 px-12">
        {/* The shard's own logo, if it has uploaded one — public and anonymous, so it brands the page
            before anyone signs in. Absent by default, so a fresh shard just shows the wordmark title. */}
        {logoUrl !== undefined && (
          <img src={logoUrl} alt={t('login.logoAlt')} className="mb-1 max-h-16 max-w-[220px] object-contain" />
        )}

        <h1 className="font-display text-[25px] font-bold tracking-wider text-gold">{t('login.title')}</h1>
        <p className="text-sm text-muted">{t('login.subtitle')}</p>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="username">{t('login.account')}</Label>
          <Input
            id="username"
            value={username}
            autoComplete="username"
            onChange={(e) => setUsername(e.target.value)}
            required
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="password">{t('login.password')}</Label>
          <Input
            id="password"
            type="password"
            value={password}
            autoComplete="current-password"
            onChange={(e) => setPassword(e.target.value)}
            required
          />
        </div>

        <Label htmlFor="remember" className="flex items-center gap-2 text-sm font-normal text-muted">
          <input
            id="remember"
            type="checkbox"
            checked={remember}
            onChange={(e) => setRemember(e.target.checked)}
            className="size-[15px] appearance-none rounded-control border border-border-strong checked:bg-gold"
          />
          {t('login.remember')}
        </Label>

        {error !== null && (
          <p role="alert" className="text-sm text-danger-text">
            {error}
          </p>
        )}

        <Button type="submit" disabled={busy} className="py-3 text-sm tracking-[0.14em]">
          {t('login.submit')}
        </Button>

        <p className="border-t border-border-subtle pt-4 text-xs leading-relaxed text-faint">
          {t('login.staffNote')}
        </p>

        {/* Data from the anonymous /api/v1/version, not UI copy — no i18n key needed. Rendered only once
            it resolves, so an unreached shard shows nothing rather than "undefined". */}
        {version.data?.version !== undefined && (
          <p className="text-xs text-faint">
            {version.data.shardName} · v{version.data.version}
          </p>
        )}
      </form>
    </div>
  )
}
