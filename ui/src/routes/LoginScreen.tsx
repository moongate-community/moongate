import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { useTranslation } from 'react-i18next'
import { ApiError } from '../lib/api'
import { useSession } from '../lib/auth'
import { useServerInfo } from '../lib/queries'
import { PublicAuthLayout } from '../components/PublicAuthLayout'
import { Button } from '../components/ui/button'
import { Input } from '../components/ui/input'
import { Label } from '../components/ui/label'

export function LoginScreen() {
  const { t } = useTranslation()
  const { signIn } = useSession()
  const serverInfo = useServerInfo()

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

  return (
    <PublicAuthLayout>
      <form onSubmit={submit} className="flex flex-col gap-4">
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

        <p className="border-t border-border-subtle pt-4 text-xs leading-relaxed text-faint">{t('login.staffNote')}</p>

        {serverInfo.data?.registrationEnabled === true && (
          <Link to="/register" className="text-center text-sm text-gold hover:text-gold-hi">
            {t('login.createAccount')}
          </Link>
        )}
      </form>
    </PublicAuthLayout>
  )
}
