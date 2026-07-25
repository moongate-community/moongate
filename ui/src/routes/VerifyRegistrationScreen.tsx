import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router'
import { useTranslation } from 'react-i18next'
import { PublicAuthLayout } from '../components/PublicAuthLayout'
import { Button } from '../components/ui/button'
import { ApiError } from '../lib/api'
import { useVerifyRegistration } from '../lib/registration'

type VerificationState = 'verifying' | 'verified' | 'expired' | 'invalid' | 'unavailable'

export function VerifyRegistrationScreen() {
  const { t } = useTranslation()
  const location = useLocation()
  const navigate = useNavigate()
  const { mutateAsync, reset } = useVerifyRegistration()
  const [token] = useState(() => new URLSearchParams(location.search).get('token') ?? '')
  const [state, setState] = useState<VerificationState>(token === '' ? 'invalid' : 'verifying')
  const started = useRef(false)

  const verify = useCallback(async () => {
    setState('verifying')

    try {
      await mutateAsync({ token })
      reset()
      setState('verified')
    } catch (caught) {
      reset()

      if (caught instanceof ApiError && caught.status === 410) {
        setState('expired')
        return
      }

      if (caught instanceof ApiError && caught.status === 400) {
        setState('invalid')
        return
      }

      setState('unavailable')
    }
  }, [mutateAsync, reset, token])

  useEffect(() => {
    navigate('/verify', { replace: true })

    if (started.current) {
      return
    }

    started.current = true
    if (token === '') {
      setState('invalid')
      return
    }

    void verify()
  }, [navigate, token, verify])

  return (
    <PublicAuthLayout>
      <section className="flex flex-col gap-4">
        <div role="status" aria-live="polite" aria-atomic="true" className="flex flex-col gap-4">
          {state === 'verifying' && (
            <>
              <h1 className="font-display text-[25px] font-bold tracking-wider text-gold">
                {t('register.verify.verifyingTitle')}
              </h1>
              <p className="text-sm text-muted">{t('register.verify.verifyingBody')}</p>
            </>
          )}

          {state === 'verified' && (
            <>
              <h1 className="font-display text-[25px] font-bold tracking-wider text-gold">
                {t('register.verify.verifiedTitle')}
              </h1>
              <p className="text-sm text-muted">{t('register.verify.verifiedBody')}</p>
              <Button asChild className="py-3 text-sm tracking-[0.14em]">
                <Link to="/login">{t('register.verify.signIn')}</Link>
              </Button>
            </>
          )}

          {state === 'expired' && (
            <>
              <h1 className="font-display text-[25px] font-bold tracking-wider text-gold">
                {t('register.verify.expiredTitle')}
              </h1>
              <p className="text-sm text-muted">{t('register.verify.expiredBody')}</p>
              <Button asChild className="py-3 text-sm tracking-[0.14em]">
                <Link to="/register/pending">{t('register.verify.resend')}</Link>
              </Button>
            </>
          )}

          {state === 'invalid' && (
            <>
              <h1 className="font-display text-[25px] font-bold tracking-wider text-gold">
                {t('register.verify.invalidTitle')}
              </h1>
              <p className="text-sm text-muted">{t('register.verify.invalidBody')}</p>
              <Button asChild className="py-3 text-sm tracking-[0.14em]">
                <Link to="/register/pending">{t('register.verify.resend')}</Link>
              </Button>
              <Link to="/login" className="text-center text-sm text-gold hover:text-gold-hi">
                {t('register.verify.signIn')}
              </Link>
            </>
          )}

          {state === 'unavailable' && (
            <>
              <h1 className="font-display text-[25px] font-bold tracking-wider text-gold">
                {t('register.verify.unavailableTitle')}
              </h1>
              <p className="text-sm text-muted">{t('register.verify.unavailableBody')}</p>
              <Button type="button" onClick={() => void verify()} className="py-3 text-sm tracking-[0.14em]">
                {t('register.verify.retry')}
              </Button>
            </>
          )}
        </div>
      </section>
    </PublicAuthLayout>
  )
}
