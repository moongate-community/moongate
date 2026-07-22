import { useState, type FormEvent } from 'react'
import { Navigate } from 'react-router'
import { useTranslation } from 'react-i18next'
import { ApiError } from '../lib/api'
import { useSession } from '../lib/auth'
import { Button } from '../components/ui/button'
import { Input } from '../components/ui/input'
import { Label } from '../components/ui/label'

export function LoginScreen() {
  const { t } = useTranslation()
  const { status, signIn } = useSession()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setBusy(true)

    try {
      await signIn(username, password)
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
      <div className="hidden flex-1 items-end bg-gradient-to-b from-raised to-deep p-10 lg:flex">
        <p className="font-display text-[15px] text-gold">&ldquo;Sosaria non dorme mai.&rdquo;</p>
      </div>

      <form onSubmit={submit} className="flex w-full max-w-[420px] flex-col justify-center gap-4 px-12">
        <h1 className="font-display text-[22px] font-bold tracking-wider text-gold">{t('login.title')}</h1>
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

        {error !== null && (
          <p role="alert" className="text-sm text-danger-text">
            {error}
          </p>
        )}

        <Button type="submit" disabled={busy}>
          {t('login.submit')}
        </Button>

        <p className="border-t border-border-subtle pt-4 text-xs leading-relaxed text-faint">
          {t('login.staffNote')}
        </p>
      </form>
    </div>
  )
}
