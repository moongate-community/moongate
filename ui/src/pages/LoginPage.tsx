import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button, Input } from '@heroui/react'
import { api } from '../api/client'
import { useAuthStore } from '../store/authStore'
import type { AuthUser } from '../store/authStore'

export function LoginPage() {
  const navigate = useNavigate()
  const login = useAuthStore((s) => s.login)

  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      const user = await api.post<AuthUser>('/auth/login', { username, password })
      login(user)
      navigate('/dashboard', { replace: true })
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Authentication failed')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="flex items-center justify-center min-h-screen">
      {/* background grid */}
      <div className="absolute inset-0 pointer-events-none" style={{
        backgroundImage: `
          linear-gradient(rgba(106,165,218,0.04) 1px, transparent 1px),
          linear-gradient(90deg, rgba(106,165,218,0.04) 1px, transparent 1px)
        `,
        backgroundSize: '48px 48px',
      }} />

      {/* corner decorations */}
      {[
        'top-8 left-8 border-t border-l',
        'top-8 right-8 border-t border-r',
        'bottom-8 left-8 border-b border-l',
        'bottom-8 right-8 border-b border-r',
      ].map((cls) => (
        <div key={cls} className={`absolute ${cls} opacity-20`}
          style={{ width: 28, height: 28, borderColor: '#6aa5da' }} />
      ))}

      {/* card */}
      <div className="relative w-full max-w-sm animate-fade-in" style={{
        background: 'rgba(36, 33, 48, 0.85)',
        border: '1px solid rgba(106,165,218,0.2)',
        borderRadius: '12px',
        boxShadow: '0 0 0 1px rgba(106,165,218,0.05), 0 24px 64px rgba(0,0,0,0.5)',
        backdropFilter: 'blur(16px)',
      }}>
        {/* top accent */}
        <div style={{
          position: 'absolute', top: 0, left: '15%', right: '15%', height: '1px',
          background: 'linear-gradient(90deg, transparent, #6aa5da, transparent)',
          borderRadius: '1px',
        }} />

        <div className="px-8 pt-9 pb-8">
          <div className="text-center mb-8">
            <div className="flex justify-center mb-4">
              <span style={{
                fontSize: '32px',
                filter: 'drop-shadow(0 0 12px rgba(106,165,218,0.6))',
                display: 'block', lineHeight: 1,
              }}>🌙</span>
            </div>
            <h1 className="font-cinzel font-semibold tracking-widest uppercase mb-1"
              style={{ color: '#6aa5da', fontSize: '18px', letterSpacing: '0.25em' }}>
              Moongate
            </h1>
            <p className="font-mono text-xs tracking-widest"
              style={{ color: 'rgba(185,187,211,0.4)', letterSpacing: '0.2em' }}>
              ADMIN PANEL
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
              <div className="flex items-center gap-2 px-3 py-2 rounded-md font-mono text-xs"
                style={{
                  background: 'rgba(239,68,68,0.08)',
                  border: '1px solid rgba(239,68,68,0.2)',
                  color: '#ef4444', letterSpacing: '0.05em',
                }}>
                <span>⚠</span><span>{error}</span>
              </div>
            )}

            <Button
              type="submit"
              color="primary"
              isLoading={loading}
              fullWidth
              className="font-mono tracking-widest uppercase text-sm mt-2"
              style={{ letterSpacing: '0.18em', height: '44px' }}
            >
              {loading ? 'Authenticating...' : 'Enter'}
            </Button>
          </form>
        </div>
      </div>
    </div>
  )
}
