import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button, Input } from '@heroui/react'
import { api } from '../api/client'
import { usePortalAuthStore } from '../store/portalAuthStore'
import type { AuthUser } from '../store/authStore'
import { ThemeToggle } from '../components/ThemeToggle'

interface ServerVersion {
  version: string
  codename: string
}

export function PortalLoginPage() {
  const navigate = useNavigate()
  const login = usePortalAuthStore((s) => s.login)
  const user = usePortalAuthStore((s) => s.user)

  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [serverVersion, setServerVersion] = useState<ServerVersion | null>(null)

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

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setLoading(true)

    try {
      const authenticatedUser = await api.post<AuthUser>('/auth/login', { username, password })
      login(authenticatedUser)
      navigate('/portal/account', { replace: true })
    } catch (submitError) {
      setError(submitError instanceof Error ? submitError.message : 'Authentication failed')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center px-6">
      <div className="fixed top-4 right-4 z-10">
        <ThemeToggle className="px-3 py-2" />
      </div>

      <div
        className="relative w-full max-w-md overflow-hidden rounded-xl border animate-fade-in"
        style={{
          background: 'linear-gradient(180deg, rgba(22,24,36,0.92), rgba(29,23,34,0.92))',
          borderColor: 'rgba(196,154,94,0.25)',
          boxShadow: '0 24px 64px rgba(0,0,0,0.45)',
          backdropFilter: 'blur(16px)',
        }}
      >
        <div
          className="absolute inset-x-0 top-0 h-px"
          style={{ background: 'linear-gradient(90deg, transparent, rgba(196,154,94,0.85), transparent)' }}
        />

        <div className="px-8 py-9">
          <div className="mb-8 text-center">
            <p
              className="mb-2 font-mono text-[11px] uppercase tracking-[0.35em]"
              style={{ color: 'rgba(249,244,237,0.45)' }}
            >
              Player Portal
            </p>
            <h1 className="font-cinzel text-2xl font-semibold" style={{ color: '#f4d6a0' }}>
              Account Access
            </h1>
            <p className="mt-3 font-mono text-xs leading-6" style={{ color: 'rgba(249,244,237,0.62)' }}>
              Review your account and characters. Trading and marketplace features will attach here later.
            </p>
          </div>

          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            <Input
              label="Username"
              value={username}
              onValueChange={setUsername}
              autoComplete="username"
              isRequired
              classNames={{
                inputWrapper: 'bg-content2 border-divider data-[hover=true]:border-primary',
                label: 'text-default-400 font-mono text-xs tracking-wider',
                input: 'font-mono text-sm',
              }}
            />
            <Input
              label="Password"
              type="password"
              value={password}
              onValueChange={setPassword}
              autoComplete="current-password"
              isRequired
              classNames={{
                inputWrapper: 'bg-content2 border-divider data-[hover=true]:border-primary',
                label: 'text-default-400 font-mono text-xs tracking-wider',
                input: 'font-mono text-sm',
              }}
            />

            {error && (
              <div
                className="rounded-md px-3 py-2 font-mono text-xs"
                style={{
                  background: 'rgba(239,68,68,0.08)',
                  border: '1px solid rgba(239,68,68,0.2)',
                  color: '#ef4444',
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
              style={{ height: '44px' }}
            >
              {loading ? 'Authenticating...' : 'Open Account'}
            </Button>
          </form>
        </div>
      </div>

      {serverVersion && (
        <div
          className="fixed bottom-4 right-4 rounded-md px-3 py-2 font-mono text-xs"
          style={{
            color: 'rgba(185,187,211,0.85)',
            background: 'rgba(12,17,24,0.75)',
            border: '1px solid rgba(196,154,94,0.22)',
            letterSpacing: '0.04em',
          }}
        >
          v{serverVersion.version} · {serverVersion.codename}
        </div>
      )}
    </div>
  )
}
