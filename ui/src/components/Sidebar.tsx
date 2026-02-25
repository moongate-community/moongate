import { NavLink, useNavigate } from 'react-router-dom'

import { useAuthStore } from '../store/authStore'

const navItems = [
  { label: 'Dashboard', path: '/dashboard', icon: '◈' },
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
        background: 'linear-gradient(180deg, #090e1a 0%, #080c14 100%)',
        borderRight: '1px solid #1e2840',
      }}
    >
      {/* top amber line */}
      <div
        className="absolute top-0 left-0 right-0 h-px"
        style={{ background: 'linear-gradient(90deg, transparent, #f0a014 50%, transparent)' }}
      />

      {/* Logo */}
      <div className="px-5 pt-7 pb-5">
        <div className="flex items-center gap-2.5 mb-1">
          <span
            style={{
              color: '#f0a014',
              fontSize: '18px',
              filter: 'drop-shadow(0 0 8px rgba(240,160,20,0.7))',
              animation: 'glow-pulse 3s ease-in-out infinite',
            }}
          >
            ⬡
          </span>
          <span
            className="font-cinzel font-semibold tracking-widest text-sm uppercase"
            style={{ color: '#f0a014', letterSpacing: '0.22em' }}
          >
            Moongate
          </span>
        </div>
        <p
          className="font-mono text-xs pl-8"
          style={{ color: 'rgba(226,217,200,0.28)', letterSpacing: '0.18em' }}
        >
          ADMIN
        </p>
      </div>

      <div style={{ height: '1px', background: '#1e2840', margin: '0 12px' }} />

      {/* Nav */}
      <nav className="flex flex-col gap-0.5 px-3 py-4 flex-1">
        {navItems.map((item) => (
          <NavLink key={item.path} to={item.path} className="block">
            {({ isActive }) => (
              <div
                className="flex items-center gap-3 px-3 py-2 rounded-md transition-all duration-200 cursor-pointer"
                style={
                  isActive
                    ? {
                        background: 'rgba(240,160,20,0.08)',
                        boxShadow: 'inset 0 0 0 1px rgba(240,160,20,0.18)',
                      }
                    : {}
                }
              >
                <span
                  className="font-mono text-sm"
                  style={{ color: isActive ? '#f0a014' : 'rgba(226,217,200,0.3)' }}
                >
                  {item.icon}
                </span>
                <span
                  className="text-sm font-medium tracking-wide"
                  style={{
                    color: isActive ? '#e2d9c8' : 'rgba(226,217,200,0.45)',
                    fontFamily: 'Outfit, sans-serif',
                  }}
                >
                  {item.label}
                </span>
                {isActive && (
                  <div
                    className="ml-auto w-1 h-1 rounded-full"
                    style={{ background: '#f0a014', boxShadow: '0 0 5px #f0a014' }}
                  />
                )}
              </div>
            )}
          </NavLink>
        ))}
      </nav>

      <div style={{ height: '1px', background: '#1e2840', margin: '0 12px' }} />

      {/* User + logout */}
      <div className="px-4 py-4 flex flex-col gap-3">
        {user && (
          <div className="flex flex-col gap-1.5">
            <div className="flex items-center gap-2">
              <div
                style={{
                  width: '6px',
                  height: '6px',
                  borderRadius: '50%',
                  background: '#22c55e',
                  boxShadow: '0 0 6px #22c55e',
                  flexShrink: 0,
                }}
              />
              <span
                className="font-mono text-xs truncate"
                style={{ color: 'rgba(226,217,200,0.65)' }}
              >
                {user.username}
              </span>
            </div>
            <div className="pl-4">
              <span
                className="font-mono text-xs px-2 py-0.5 rounded"
                style={{
                  background: 'rgba(240,160,20,0.08)',
                  border: '1px solid rgba(240,160,20,0.18)',
                  color: '#f0a014',
                  letterSpacing: '0.12em',
                }}
              >
                {user.role.toUpperCase()}
              </span>
            </div>
          </div>
        )}

        <button
          onClick={handleLogout}
          className="w-full flex items-center gap-2.5 px-3 py-2 rounded-md text-sm transition-all duration-200"
          style={{ color: 'rgba(226,217,200,0.35)', background: 'transparent', border: '1px solid transparent', fontFamily: 'Outfit, sans-serif' }}
          onMouseEnter={(e) => {
            e.currentTarget.style.color = '#ef4444'
            e.currentTarget.style.borderColor = 'rgba(239,68,68,0.18)'
            e.currentTarget.style.background = 'rgba(239,68,68,0.05)'
          }}
          onMouseLeave={(e) => {
            e.currentTarget.style.color = 'rgba(226,217,200,0.35)'
            e.currentTarget.style.borderColor = 'transparent'
            e.currentTarget.style.background = 'transparent'
          }}
        >
          <span className="font-mono text-xs">⏻</span>
          <span className="tracking-wide">Logout</span>
        </button>
      </div>

      {/* bottom amber line */}
      <div
        className="absolute bottom-0 left-0 right-0 h-px"
        style={{ background: 'linear-gradient(90deg, transparent, rgba(240,160,20,0.25) 50%, transparent)' }}
      />
    </aside>
  )
}
