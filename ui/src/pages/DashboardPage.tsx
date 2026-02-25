import { useEffect, useState } from 'react'
import { api } from '../api/client'

interface StatCard {
  label: string
  value: string | null
  status: 'loading' | 'ok' | 'error'
  icon: string
}

export function DashboardPage() {
  const [health, setHealth] = useState<string | null>(null)
  const [healthStatus, setHealthStatus] = useState<'loading' | 'ok' | 'error'>('loading')

  useEffect(() => {
    api.get<string>('/health')
      .then((res) => { setHealth(res); setHealthStatus('ok') })
      .catch(() => setHealthStatus('error'))
  }, [])

  const stats: StatCard[] = [
    {
      label: 'Server Health',
      value: health,
      status: healthStatus,
      icon: '◎',
    },
  ]

  return (
    <div className="flex flex-col gap-8 animate-fade-in">
      {/* Page header */}
      <div>
        <div className="flex items-center gap-3 mb-1">
          <div
            style={{ width: '2px', height: '20px', background: '#f0a014', borderRadius: '1px', boxShadow: '0 0 6px rgba(240,160,20,0.5)' }}
          />
          <h1
            className="font-cinzel font-semibold tracking-wider"
            style={{ color: '#e2d9c8', fontSize: '18px', letterSpacing: '0.12em' }}
          >
            Dashboard
          </h1>
        </div>
        <p
          className="font-mono text-xs pl-5"
          style={{ color: 'rgba(226,217,200,0.3)', letterSpacing: '0.1em' }}
        >
          SYSTEM OVERVIEW
        </p>
      </div>

      {/* Stats grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        {stats.map((stat) => (
          <div
            key={stat.label}
            className="relative p-5 rounded-xl transition-all duration-300"
            style={{
              background: '#0d1220',
              border: '1px solid #1e2840',
              boxShadow: '0 4px 24px rgba(0,0,0,0.3)',
            }}
          >
            {/* top accent line */}
            <div
              style={{
                position: 'absolute',
                top: 0,
                left: '20%',
                right: '20%',
                height: '1px',
                background: stat.status === 'ok'
                  ? 'linear-gradient(90deg, transparent, #22c55e, transparent)'
                  : stat.status === 'error'
                  ? 'linear-gradient(90deg, transparent, #ef4444, transparent)'
                  : 'linear-gradient(90deg, transparent, #1e2840, transparent)',
                borderRadius: '1px',
              }}
            />

            <div className="flex items-start justify-between mb-4">
              <span
                className="font-mono text-xs tracking-widest uppercase"
                style={{ color: 'rgba(226,217,200,0.4)', letterSpacing: '0.14em' }}
              >
                {stat.label}
              </span>
              <span
                className="font-mono text-base"
                style={{
                  color: stat.status === 'ok' ? '#22c55e'
                    : stat.status === 'error' ? '#ef4444'
                    : 'rgba(226,217,200,0.2)',
                }}
              >
                {stat.icon}
              </span>
            </div>

            <div className="flex items-end gap-2">
              {stat.status === 'loading' && (
                <div className="flex gap-1 items-center">
                  {[0, 1, 2].map((i) => (
                    <div
                      key={i}
                      className="w-1 h-1 rounded-full"
                      style={{
                        background: 'rgba(226,217,200,0.3)',
                        animation: `glow-pulse 1.2s ease-in-out ${i * 0.2}s infinite`,
                      }}
                    />
                  ))}
                </div>
              )}
              {stat.status === 'ok' && (
                <div className="flex items-center gap-2">
                  <div
                    style={{
                      width: '6px',
                      height: '6px',
                      borderRadius: '50%',
                      background: '#22c55e',
                      boxShadow: '0 0 8px #22c55e',
                    }}
                  />
                  <span
                    className="font-mono text-lg font-medium"
                    style={{ color: '#e2d9c8' }}
                  >
                    {stat.value ?? 'ok'}
                  </span>
                </div>
              )}
              {stat.status === 'error' && (
                <div className="flex items-center gap-2">
                  <div
                    style={{
                      width: '6px',
                      height: '6px',
                      borderRadius: '50%',
                      background: '#ef4444',
                      boxShadow: '0 0 8px #ef4444',
                    }}
                  />
                  <span
                    className="font-mono text-sm"
                    style={{ color: '#ef4444' }}
                  >
                    OFFLINE
                  </span>
                </div>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
