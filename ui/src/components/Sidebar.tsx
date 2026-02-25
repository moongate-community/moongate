import { useNavigate, NavLink } from 'react-router-dom'
import { Button, Chip, Divider } from '@heroui/react'
import { useAuthStore } from '../store/authStore'

const navItems = [
  { label: 'Dashboard', path: '/dashboard' },
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
    <aside className="flex flex-col w-60 min-h-screen bg-content1 border-r border-divider px-3 py-4 shrink-0">
      {/* Logo */}
      <div className="px-3 mb-6">
        <h2 className="text-xl font-bold">🌙 Moongate</h2>
        <p className="text-default-400 text-xs mt-0.5">Admin Panel</p>
      </div>

      <Divider className="mb-4" />

      {/* Nav */}
      <nav className="flex flex-col gap-1 flex-1">
        {navItems.map((item) => (
          <NavLink key={item.path} to={item.path}>
            {({ isActive }) => (
              <Button
                variant={isActive ? 'flat' : 'light'}
                color={isActive ? 'primary' : 'default'}
                className="w-full justify-start"
                size="sm"
              >
                {item.label}
              </Button>
            )}
          </NavLink>
        ))}
      </nav>

      <Divider className="my-4" />

      {/* User info + logout */}
      <div className="flex flex-col gap-2 px-1">
        {user && (
          <div className="flex items-center gap-2">
            <span className="text-sm text-default-600 truncate">{user.username}</span>
            <Chip size="sm" variant="flat" color="secondary" className="shrink-0">
              {user.role}
            </Chip>
          </div>
        )}
        <Button
          variant="flat"
          color="danger"
          size="sm"
          className="w-full"
          onPress={handleLogout}
        >
          Logout
        </Button>
      </div>
    </aside>
  )
}
