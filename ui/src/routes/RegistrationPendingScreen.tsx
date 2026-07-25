import { useEffect, useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router'
import { useTranslation } from 'react-i18next'
import { PublicAuthLayout } from '../components/PublicAuthLayout'
import { Button } from '../components/ui/button'
import { Input } from '../components/ui/input'
import { Label } from '../components/ui/label'
import { ApiError } from '../lib/api'
import {
  useResendVerification,
  validateResend,
  type RegistrationField,
  type RegistrationValidationErrors,
} from '../lib/registration'

type PendingState = { username?: unknown; email?: unknown }

export function RegistrationPendingScreen() {
  const { t } = useTranslation()
  const location = useLocation()
  const navigate = useNavigate()
  const resendVerification = useResendVerification()
  const state = (location.state ?? {}) as PendingState
  const initialUsername = typeof state.username === 'string' ? state.username : ''
  const initialEmail = typeof state.email === 'string' ? state.email : ''

  const [username, setUsername] = useState(initialUsername)
  const [email, setEmail] = useState(initialEmail)
  const [fieldErrors, setFieldErrors] = useState<RegistrationValidationErrors>({})
  const [formMessage, setFormMessage] = useState<string | null>(null)

  useEffect(() => {
    if (location.state !== null) {
      navigate(`${location.pathname}${location.search}${location.hash}`, { replace: true, state: null })
    }
  }, [location.hash, location.pathname, location.search, location.state, navigate])

  function fieldMessage(field: RegistrationField): string {
    return t(`register.errors.${field}`)
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFormMessage(null)

    const errors = validateResend({ username, email })
    setFieldErrors(errors)
    if (Object.keys(errors).length > 0) {
      return
    }

    try {
      await resendVerification.mutateAsync({ username: username.trim(), email: email.trim() })
      setFormMessage(t('register.pending.confirmation'))
    } catch (caught) {
      setFormMessage(
        caught instanceof ApiError
          ? ({
              400: t('register.pending.errors.invalidDetails'),
              429: t('register.pending.errors.rateLimited'),
              503: t('register.pending.errors.emailUnavailable'),
            }[caught.status] ?? t('error.generic'))
          : t('error.generic'),
      )
    }
  }

  return (
    <PublicAuthLayout>
      <section aria-labelledby="registration-pending-title" className="flex flex-col gap-4">
        <h1 id="registration-pending-title" className="font-display text-[25px] font-bold tracking-wider text-gold">
          {t('register.pending.title')}
        </h1>
        <p className="text-sm text-muted">{t('register.pending.guidance')}</p>

        <form aria-labelledby="resend-title" onSubmit={submit} noValidate className="flex flex-col gap-4">
          <h2 id="resend-title" className="font-display text-lg font-bold tracking-wide text-gold">
            {t('register.pending.resendTitle')}
          </h2>

          <ResendFieldControl
            id="username"
            label={t('register.username')}
            value={username}
            autoComplete="username"
            error={fieldErrors.username !== undefined}
            errorMessage={fieldErrors.username === undefined ? undefined : fieldMessage('username')}
            onChange={setUsername}
          />
          <ResendFieldControl
            id="email"
            label={t('register.email')}
            type="email"
            value={email}
            autoComplete="email"
            error={fieldErrors.email !== undefined}
            errorMessage={fieldErrors.email === undefined ? undefined : fieldMessage('email')}
            onChange={setEmail}
          />

          <p role="status" aria-live="polite" aria-atomic="true" className="text-sm text-muted">
            {formMessage ?? ''}
          </p>

          <Button type="submit" disabled={resendVerification.isPending} className="py-3 text-sm tracking-[0.14em]">
            {t('register.pending.resendSubmit')}
          </Button>
        </form>

        <Link to="/login" className="text-center text-sm text-gold hover:text-gold-hi">
          {t('common.backToLogin')}
        </Link>
      </section>
    </PublicAuthLayout>
  )
}

type ResendFieldControlProps = {
  id: 'username' | 'email'
  label: string
  type?: 'email' | 'text'
  value: string
  autoComplete: 'email' | 'username'
  error: boolean
  errorMessage: string | undefined
  onChange: (value: string) => void
}

function ResendFieldControl({
  id,
  label,
  type = 'text',
  value,
  autoComplete,
  error,
  errorMessage,
  onChange,
}: ResendFieldControlProps) {
  const errorId = `${id}-error`

  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        type={type}
        value={value}
        autoComplete={autoComplete}
        aria-invalid={error}
        aria-describedby={error ? errorId : undefined}
        onChange={(event) => onChange(event.target.value)}
        required
      />
      {errorMessage !== undefined && (
        <p id={errorId} role="alert" className="text-sm text-danger-text">
          {errorMessage}
        </p>
      )}
    </div>
  )
}
