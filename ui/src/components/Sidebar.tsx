import { NavLink, useNavigate } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'
import { ThemeToggle } from './ThemeToggle'

const navItems = [
  { label: 'Dashboard', path: '/dashboard', icon: '◈' },
  { label: 'Users', path: '/users', icon: '⍟' },
  { label: 'Help Tickets', path: '/help-tickets', icon: '✉' },
  { label: 'Console', path: '/console', icon: '⌘' },
  { label: 'Active Players', path: '/active-players', icon: '◎' },
  { label: 'Maps', path: '/maps', icon: '◫' },
  { label: 'Item Templates', path: '/item-templates', icon: '▦' },
]

export function Sidebar() {
  const navigate = useNavigate()
  const user = useAuthStore((s) => s.user)
  const logout = useAuthStore((s) => s.logout)

  function handleLogout() {
    logout()
    navigate('/login', { replace: true })
  }

  return (
    <aside
      className="flex flex-col w-56 min-h-screen shrink-0 relative"
      style={{
        background: 'var(--mg-panel)',
        borderRight: '1px solid var(--mg-border)',
        backdropFilter: 'blur(12px)',
      }}
    >
      {/* top accent line */}
      <div style={{
        position: 'absolute', top: 0, left: 0, right: 0, height: '1px',
        background: 'linear-gradient(90deg, transparent, #6aa5da, transparent)',
      }} />

      {/* Logo */}
      <div className="px-5 pt-7 pb-5">
        <div className="flex items-center gap-2.5 mb-1">
          <span style={{
            color: 'var(--mg-accent)',
            fontSize: '18px',
            filter: 'drop-shadow(0 0 8px rgba(106,165,218,0.7))',
            animation: 'glow-pulse 3s ease-in-out infinite',
          }}>
            🌙
          </span>
          <span className="font-cinzel font-semibold tracking-widest text-sm uppercase"
            style={{ color: 'var(--mg-accent)', letterSpacing: '0.22em' }}>
            Moongate
          </span>
        </div>
        <p className="font-mono text-xs pl-8"
          style={{ color: 'var(--mg-muted)', letterSpacing: '0.18em' }}>
          ADMIN
        </p>
      </div>

      <div style={{ height: '1px', background: 'var(--mg-border)', margin: '0 12px' }} />

      {/* Nav */}
      <nav className="flex flex-col gap-0.5 px-3 py-4 flex-1">
        {navItems.map((item) => (
          <NavLink key={item.path} to={item.path} className="block">
            {({ isActive }) => (
              <div
                className="flex items-center gap-3 px-3 py-2 rounded-md transition-all duration-200 cursor-pointer"
                style={isActive ? {
                  background: 'rgba(106,165,218,0.1)',
                  boxShadow: 'inset 0 0 0 1px var(--mg-border)',
                } : {}}
              >
                <span className="font-mono text-sm"
                  style={{ color: isActive ? 'var(--mg-accent)' : 'var(--mg-muted)' }}>
                  {item.icon}
                </span>
                <span className="text-sm font-medium tracking-wide"
                  style={{
                    color: isActive ? 'var(--mg-text)' : 'var(--mg-muted)',
                    fontFamily: 'Outfit, sans-serif',
                  }}>
                  {item.label}
                </span>
                {isActive && (
                  <div className="ml-auto w-1 h-1 rounded-full"
                    style={{ background: 'var(--mg-accent)', boxShadow: '0 0 5px var(--mg-accent)' }} />
                )}
              </div>
            )}
          </NavLink>
        ))}
      </nav>

      <div style={{ height: '1px', background: 'var(--mg-border)', margin: '0 12px' }} />

      {/* User + logout */}
      <div className="px-4 py-4 flex flex-col gap-3">
        {user && (
          <div className="flex flex-col gap-1.5">
            <div className="flex items-center gap-2">
              <div style={{
                width: '6px', height: '6px', borderRadius: '50%',
                background: '#22c55e', boxShadow: '0 0 6px #22c55e', flexShrink: 0,
              }} />
              <span className="font-mono text-xs truncate"
                style={{ color: 'var(--mg-text)' }}>
                {user.username}
              </span>
            </div>
            <div className="pl-4">
              <span className="font-mono text-xs px-2 py-0.5 rounded"
                style={{
                  background: 'rgba(106,165,218,0.1)',
                  border: '1px solid var(--mg-border)',
                  color: 'var(--mg-accent)',
                  letterSpacing: '0.12em',
                }}>
                {user.role.toUpperCase()}
              </span>
            </div>
          </div>
        )}

        <ThemeToggle className="w-full px-3 py-2" />

        <button
          onClick={handleLogout}
          className="w-full flex items-center gap-2.5 px-3 py-2 rounded-md text-sm transition-all duration-200"
          style={{
            color: 'var(--mg-muted)',
            background: 'transparent',
            border: '1px solid transparent',
            fontFamily: 'Outfit, sans-serif',
          }}
          onMouseEnter={(e) => {
            e.currentTarget.style.color = '#ef4444'
            e.currentTarget.style.borderColor = 'rgba(239,68,68,0.2)'
            e.currentTarget.style.background = 'rgba(239,68,68,0.06)'
          }}
          onMouseLeave={(e) => {
            e.currentTarget.style.color = 'var(--mg-muted)'
            e.currentTarget.style.borderColor = 'transparent'
            e.currentTarget.style.background = 'transparent'
          }}
        >
          <span className="font-mono text-xs">⏻</span>
          <span className="tracking-wide">Logout</span>
        </button>
      </div>

      <div style={{
        position: 'absolute', bottom: 0, left: 0, right: 0, height: '1px',
        background: 'linear-gradient(90deg, transparent, var(--mg-border), transparent)',
      }} />
    </aside>
  )
}
