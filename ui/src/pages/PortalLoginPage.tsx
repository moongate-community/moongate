import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button, Input } from '@heroui/react'
import { useIntl } from 'react-intl'
import { api } from '../api/client'
import { usePortalAuthStore } from '../store/portalAuthStore'
import type { AuthUser } from '../store/authStore'
import { ThemeToggle } from '../components/ThemeToggle'
import type { PublicBranding } from '../types/publicBranding'
import { PortalLanguageSwitcher } from '../components/PortalLanguageSwitcher'

interface ServerVersion {
  version: string
  codename: string
}

export function PortalLoginPage() {
  const navigate = useNavigate()
  const intl = useIntl()
  const login = usePortalAuthStore((s) => s.login)
  const user = usePortalAuthStore((s) => s.user)

  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [serverVersion, setServerVersion] = useState<ServerVersion | null>(null)
  const [branding, setBranding] = useState<PublicBranding | null>(null)

  useEffect(() => {
    if (user) {
      navigate('/portal/account', { replace: true })
    }
  }, [navigate, user])

  useEffect(() => {
    let isCancelled = false

    api.get<ServerVersion>('/version')
      .then((value) => {
        if (!isCancelled) {
          setServerVersion(value)
        }
      })
      .catch(() => {
        if (!isCancelled) {
          setServerVersion(null)
        }
      })

    return () => {
      isCancelled = true
    }
  }, [])

  useEffect(() => {
    let isCancelled = false

    api.get<PublicBranding>('/branding')
      .then((value) => {
        if (!isCancelled) {
          setBranding(value)
        }
      })
      .catch(() => {
        if (!isCancelled) {
          setBranding(null)
        }
      })

    return () => {
      isCancelled = true
    }
  }, [])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setLoading(true)

    try {
      const authenticatedUser = await api.post<AuthUser>('/auth/login', { username, password })
      login(authenticatedUser)
      navigate('/portal/account', { replace: true })
    } catch (submitError) {
      setError(submitError instanceof Error ? submitError.message : intl.formatMessage({ id: 'portal.login.error' }))
    } finally {
      setLoading(false)
    }
  }

  return (
    <div
      className="flex min-h-screen items-center justify-center px-6"
      style={{
        background: [
          'radial-gradient(circle at top, rgba(214,179,106,0.12), transparent 34%)',
          'radial-gradient(circle at bottom, rgba(143,107,47,0.12), transparent 28%)',
          'linear-gradient(180deg, #19140f 0%, #241b14 42%, #16110d 100%)',
        ].join(', '),
      }}
    >
      <div className="fixed top-4 right-4 z-10">
        <div className="flex items-center gap-2">
          <PortalLanguageSwitcher />
          <ThemeToggle className="px-3 py-2" />
        </div>
      </div>

      <div
        className="relative w-full max-w-md overflow-hidden rounded-xl border animate-fade-in"
        style={{
          background: 'linear-gradient(180deg, rgba(44,32,22,0.94), rgba(28,22,17,0.96))',
          borderColor: 'rgba(214,179,106,0.24)',
          boxShadow: '0 30px 80px rgba(0,0,0,0.46), inset 0 1px 0 rgba(255,236,205,0.04)',
          backdropFilter: 'blur(16px)',
        }}
      >
        <div
          className="absolute inset-x-0 top-0 h-px"
          style={{ background: 'linear-gradient(90deg, transparent, rgba(214,179,106,0.9), transparent)' }}
        />

        <div className="px-8 py-9">
          <div className="mb-8 text-center">
            <div className="mb-4 flex justify-center">
              {branding?.playerLoginLogoUrl && (
                <img
                  src={branding.playerLoginLogoUrl}
                  alt="Player Login Logo"
                  style={{
                    width: '140px',
                    height: 'auto',
                    filter: 'drop-shadow(0 0 18px rgba(214,179,106,0.22))',
                  }}
                />
              )}
            </div>
            <p
              className="mb-2 font-mono text-[11px] uppercase tracking-[0.35em]"
              style={{ color: 'rgba(244,234,215,0.52)' }}
            >
              {intl.formatMessage({ id: 'portal.login.portalLabel' })}
            </p>
            <h1 className="font-cinzel text-2xl font-semibold" style={{ color: '#f4ead7' }}>
              {branding?.shardName || 'Moongate'}
            </h1>
            <p className="mt-3 font-mono text-xs leading-6" style={{ color: 'rgba(244,234,215,0.7)' }}>
              {intl.formatMessage({ id: 'portal.login.subtitle' })}
            </p>
          </div>

          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            <Input
              label={intl.formatMessage({ id: 'portal.login.username' })}
              value={username}
              onValueChange={setUsername}
              autoComplete="username"
              isRequired
              classNames={{
                inputWrapper: 'border-divider data-[hover=true]:border-warning',
                label: 'font-mono text-xs tracking-wider',
                input: 'font-mono text-sm text-[#f4ead7]',
              }}
              style={{}}
            />
            <Input
              label={intl.formatMessage({ id: 'portal.login.password' })}
              type="password"
              value={password}
              onValueChange={setPassword}
              autoComplete="current-password"
              isRequired
              classNames={{
                inputWrapper: 'border-divider data-[hover=true]:border-warning',
                label: 'font-mono text-xs tracking-wider',
                input: 'font-mono text-sm text-[#f4ead7]',
              }}
            />

            {error && (
              <div
                className="rounded-md px-3 py-2 font-mono text-xs"
                style={{
                  background: 'rgba(149, 44, 34, 0.18)',
                  border: '1px solid rgba(211, 98, 78, 0.32)',
                  color: '#f0b0a4',
                  letterSpacing: '0.05em',
                }}
              >
                {error}
              </div>
            )}

            <Button
              type="submit"
              color="warning"
              isLoading={loading}
              fullWidth
              className="mt-2 font-mono uppercase tracking-[0.18em]"
              style={{
                height: '44px',
                background: 'linear-gradient(180deg, #d6b36a 0%, #b98b3d 100%)',
                color: '#2a2118',
              }}
            >
              {loading
                ? intl.formatMessage({ id: 'portal.login.submitting' })
                : intl.formatMessage({ id: 'portal.login.submit' })}
            </Button>
          </form>
        </div>
      </div>

      {serverVersion && (
        <div
          className="fixed bottom-4 right-4 rounded-md px-3 py-2 font-mono text-xs"
          style={{
            color: 'rgba(244,234,215,0.82)',
            background: 'rgba(27,20,15,0.78)',
            border: '1px solid rgba(214,179,106,0.22)',
            letterSpacing: '0.04em',
          }}
        >
          v{serverVersion.version} · {serverVersion.codename}
        </div>
      )}
    </div>
  )
}
