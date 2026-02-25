import { useEffect, useState } from 'react'
import { api } from '../api/client'

type Status = 'loading' | 'ok' | 'error'

interface StatCard {
  label: string
  value: string | null
  status: Status
  icon: string
}

const statusColor: Record<Status, string> = {
  loading: 'rgba(185,187,211,0.25)',
  ok:      '#22c55e',
  error:   '#ef4444',
}

const statusLineColor: Record<Status, string> = {
  loading: 'linear-gradient(90deg, transparent, rgba(106,165,218,0.2), transparent)',
  ok:      'linear-gradient(90deg, transparent, #22c55e, transparent)',
  error:   'linear-gradient(90deg, transparent, #ef4444, transparent)',
}

export function DashboardPage() {
  const [health, setHealth] = useState<string | null>(null)
  const [healthStatus, setHealthStatus] = useState<Status>('loading')

  useEffect(() => {
    api.get<string>('/health')
      .then((res) => { setHealth(res); setHealthStatus('ok') })
      .catch(() => setHealthStatus('error'))
  }, [])

  const stats: StatCard[] = [
    { label: 'Server Health', value: health, status: healthStatus, icon: '◎' },
  ]

  return (
    <div className="flex flex-col gap-8 animate-fade-in">
      {/* header */}
      <div>
        <div className="flex items-center gap-3 mb-1">
          <div style={{
            width: '2px', height: '20px',
            background: '#6aa5da',
            borderRadius: '1px',
            boxShadow: '0 0 6px rgba(106,165,218,0.5)',
          }} />
          <h1 className="font-cinzel font-semibold tracking-wider"
            style={{ color: '#f9f4ed', fontSize: '18px', letterSpacing: '0.12em' }}>
            Dashboard
          </h1>
        </div>
        <p className="font-mono text-xs pl-5"
          style={{ color: 'rgba(185,187,211,0.35)', letterSpacing: '0.1em' }}>
          SYSTEM OVERVIEW
        </p>
      </div>

      {/* stats grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        {stats.map((stat) => (
          <div
            key={stat.label}
            className="relative p-5 rounded-xl"
            style={{
              background: 'rgba(36,33,48,0.7)',
              border: '1px solid rgba(106,165,218,0.15)',
              backdropFilter: 'blur(8px)',
            }}
          >
            <div style={{
              position: 'absolute', top: 0, left: '20%', right: '20%', height: '1px',
              background: statusLineColor[stat.status],
            }} />

            <div className="flex items-start justify-between mb-4">
              <span className="font-mono text-xs tracking-widest uppercase"
                style={{ color: 'rgba(185,187,211,0.45)', letterSpacing: '0.14em' }}>
                {stat.label}
              </span>
              <span className="font-mono text-base"
                style={{ color: statusColor[stat.status] }}>
                {stat.icon}
              </span>
            </div>

            <div className="flex items-center gap-2">
              {stat.status === 'loading' && (
                <>
                  {[0, 1, 2].map((i) => (
                    <div key={i} className="w-1 h-1 rounded-full"
                      style={{
                        background: 'rgba(185,187,211,0.3)',
                        animation: `glow-pulse 1.2s ease-in-out ${i * 0.2}s infinite`,
                      }} />
                  ))}
                </>
              )}
              {stat.status !== 'loading' && (
                <>
                  <div style={{
                    width: '6px', height: '6px', borderRadius: '50%', flexShrink: 0,
                    background: statusColor[stat.status],
                    boxShadow: `0 0 8px ${statusColor[stat.status]}`,
                  }} />
                  <span className="font-mono text-lg font-medium"
                    style={{ color: stat.status === 'error' ? '#ef4444' : '#f9f4ed' }}>
                    {stat.status === 'error' ? 'OFFLINE' : (stat.value ?? 'ok')}
                  </span>
                </>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
