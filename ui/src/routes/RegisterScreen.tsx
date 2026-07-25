import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { useTranslation } from 'react-i18next'
import { PublicAuthLayout } from '../components/PublicAuthLayout'
import { Button } from '../components/ui/button'
import { Input } from '../components/ui/input'
import { Label } from '../components/ui/label'
import { ApiError } from '../lib/api'
import { useServerInfo } from '../lib/queries'
import {
  useRegisterAccount,
  validateRegistration,
  type RegistrationField,
  type RegistrationValidationCode,
  type RegistrationValidationErrors,
} from '../lib/registration'

const REGISTRATION_FIELDS: RegistrationField[] = ['username', 'email', 'password', 'confirmation']

function backendFieldErrors(error: ApiError): RegistrationValidationErrors {
  const errors: RegistrationValidationErrors = {}

  for (const key of Object.keys(error.problem?.errors ?? {})) {
    const field = key.toLowerCase()
    if (REGISTRATION_FIELDS.includes(field as RegistrationField)) {
      errors[field as RegistrationField] = field === 'confirmation' ? 'mismatch' : 'invalid'
    }
  }

  return errors
}

export function RegisterScreen() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const serverInfo = useServerInfo()
  const registerAccount = useRegisterAccount()

  const [username, setUsername] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirmation, setConfirmation] = useState('')
  const [fieldErrors, setFieldErrors] = useState<RegistrationValidationErrors>({})
  const [formError, setFormError] = useState<string | null>(null)

  function fieldMessage(field: RegistrationField, code: RegistrationValidationCode): string {
    return code === 'mismatch' ? t('register.errors.confirmation') : t(`register.errors.${field}`)
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFormError(null)

    const errors = validateRegistration({ username, email, password, confirmation })
    setFieldErrors(errors)
    if (Object.keys(errors).length > 0) {
      return
    }

    try {
      await registerAccount.mutateAsync({
        username: username.trim(),
        email: email.trim(),
        password,
      })
      setPassword('')
      setConfirmation('')
      navigate('/register/pending', {
        state: { username: username.trim(), email: email.trim() },
      })
    } catch (caught) {
      if (!(caught instanceof ApiError)) {
        setFormError(t('error.generic'))
        return
      }

      if (caught.status === 400) {
        const backendErrors = backendFieldErrors(caught)
        setFieldErrors(backendErrors)
        if (Object.keys(backendErrors).length === 0) {
          setFormError(t('error.generic'))
        }
        return
      }

      const errorKey = {
        403: 'disabled',
        409: 'conflict',
        429: 'rateLimited',
        503: 'emailUnavailable',
      }[caught.status]
      setFormError(errorKey === undefined ? t('error.generic') : t(`register.errors.${errorKey}`))
    }
  }

  if (serverInfo.isError || serverInfo.isRefetchError || serverInfo.data?.registrationEnabled !== true) {
    return (
      <PublicAuthLayout>
        <h1 className="font-display text-[25px] font-bold tracking-wider text-gold">
          {t('register.unavailableTitle')}
        </h1>
        <p className="text-sm text-muted">{t('register.unavailableBody')}</p>
        <Link to="/login" className="text-sm text-gold hover:text-gold-hi">
          {t('common.backToLogin')}
        </Link>
      </PublicAuthLayout>
    )
  }

  return (
    <PublicAuthLayout>
      <form aria-labelledby="register-title" onSubmit={submit} noValidate className="flex flex-col gap-4">
        <h1 id="register-title" className="font-display text-[25px] font-bold tracking-wider text-gold">
          {t('register.title')}
        </h1>
        <p className="text-sm text-muted">{t('register.subtitle')}</p>
        <p className="text-xs leading-relaxed text-faint">{t('register.requirements')}</p>

        <RegistrationFieldControl
          id="username"
          label={t('register.username')}
          value={username}
          autoComplete="username"
          error={fieldErrors.username}
          errorMessage={fieldErrors.username === undefined ? undefined : fieldMessage('username', fieldErrors.username)}
          onChange={setUsername}
        />
        <RegistrationFieldControl
          id="email"
          label={t('register.email')}
          type="email"
          value={email}
          autoComplete="email"
          error={fieldErrors.email}
          errorMessage={fieldErrors.email === undefined ? undefined : fieldMessage('email', fieldErrors.email)}
          onChange={setEmail}
        />
        <RegistrationFieldControl
          id="password"
          label={t('register.password')}
          type="password"
          value={password}
          autoComplete="new-password"
          error={fieldErrors.password}
          errorMessage={fieldErrors.password === undefined ? undefined : fieldMessage('password', fieldErrors.password)}
          onChange={setPassword}
        />
        <RegistrationFieldControl
          id="confirmation"
          label={t('register.confirmation')}
          type="password"
          value={confirmation}
          autoComplete="new-password"
          error={fieldErrors.confirmation}
          errorMessage={
            fieldErrors.confirmation === undefined ? undefined : fieldMessage('confirmation', fieldErrors.confirmation)
          }
          onChange={setConfirmation}
        />

        {formError !== null && (
          <p aria-live="polite" aria-atomic="true" className="text-sm text-danger-text">
            {formError}
          </p>
        )}

        <Button type="submit" disabled={registerAccount.isPending} className="py-3 text-sm tracking-[0.14em]">
          {t('register.submit')}
        </Button>

        <Link to="/login" className="text-center text-sm text-gold hover:text-gold-hi">
          {t('common.backToLogin')}
        </Link>
      </form>
    </PublicAuthLayout>
  )
}

type RegistrationFieldControlProps = {
  id: RegistrationField
  label: string
  type?: 'email' | 'password' | 'text'
  value: string
  autoComplete: 'email' | 'new-password' | 'username'
  error: RegistrationValidationCode | undefined
  errorMessage: string | undefined
  onChange: (value: string) => void
}

function RegistrationFieldControl({
  id,
  label,
  type = 'text',
  value,
  autoComplete,
  error,
  errorMessage,
  onChange,
}: RegistrationFieldControlProps) {
  const errorId = `${id}-error`

  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        type={type}
        value={value}
        autoComplete={autoComplete}
        aria-invalid={error !== undefined}
        aria-describedby={error === undefined ? undefined : errorId}
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
