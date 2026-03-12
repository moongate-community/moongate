import { Avatar, Dropdown, DropdownItem, DropdownMenu, DropdownSection, DropdownTrigger } from '@heroui/react'
import { useIntl } from 'react-intl'
import { NavLink, useNavigate } from 'react-router-dom'
import { PortalLanguageSwitcher } from './PortalLanguageSwitcher'
import { ThemeToggle } from './ThemeToggle'
import { usePortalAuthStore } from '../store/portalAuthStore'

function PortalNavItem({ to, label }: { to: string, label: string }) {
  return (
    <NavLink
      to={to}
      className="rounded-md px-3 py-2 font-mono text-xs uppercase tracking-[0.18em] transition-colors"
      style={({ isActive }) => ({
        color: isActive ? '#f4ead7' : 'rgba(244,234,215,0.72)',
        background: isActive ? 'rgba(214,179,106,0.12)' : 'transparent',
        border: isActive ? '1px solid rgba(214,179,106,0.22)' : '1px solid transparent',
      })}
    >
      {label}
    </NavLink>
  )
}

export function PortalNavbar() {
  const intl = useIntl()
  const navigate = useNavigate()
  const user = usePortalAuthStore((s) => s.user)
  const logout = usePortalAuthStore((s) => s.logout)

  const initial = (user?.username?.trim()?.charAt(0) || '?').toUpperCase()

  function handleLogout() {
    logout()
    navigate('/portal/login', { replace: true })
  }

  return (
    <header
      className="sticky top-0 z-20 border-b px-6 py-4 backdrop-blur"
      style={{
        background: 'linear-gradient(180deg, rgba(31,23,17,0.94), rgba(24,18,13,0.88))',
        borderColor: 'rgba(214,179,106,0.14)',
      }}
    >
      <div className="mx-auto flex w-full max-w-[1180px] items-center justify-between gap-4">
        <div className="flex items-center gap-2">
          <PortalNavItem to="/portal/account" label={intl.formatMessage({ id: 'portal.nav.characters' })} />
        </div>

        <div className="flex items-center gap-3">
          <PortalLanguageSwitcher />
          <ThemeToggle className="px-3 py-2" />
          <Dropdown placement="bottom-end">
            <DropdownTrigger>
              <button
                type="button"
                className="rounded-full transition-transform hover:scale-[1.03] focus:outline-none"
                aria-label={intl.formatMessage({ id: 'portal.nav.profileMenu' })}
              >
                <Avatar
                  name={initial}
                  className="h-10 w-10"
                  style={{
                    background: 'linear-gradient(180deg, #d6b36a 0%, #9f7631 100%)',
                    color: '#241a12',
                  }}
                />
              </button>
            </DropdownTrigger>
            <DropdownMenu
              aria-label="Portal profile menu"
              className="min-w-[220px]"
              itemClasses={{
                base: 'font-mono text-xs',
              }}
              style={{
                background: 'linear-gradient(180deg, rgba(39,29,21,0.98), rgba(27,21,16,0.98))',
                border: '1px solid rgba(214,179,106,0.16)',
              }}
            >
              <DropdownSection
                title={user?.username ?? intl.formatMessage({ id: 'portal.account.unknown' })}
                classNames={{
                  heading: 'font-mono text-[11px] uppercase tracking-[0.18em] text-[rgba(244,234,215,0.5)]',
                }}
              >
                <DropdownItem
                  key="profile"
                  className="text-[#f4ead7]"
                  onPress={() => navigate('/portal/profile')}
                >
                  {intl.formatMessage({ id: 'portal.nav.profile' })}
                </DropdownItem>
                <DropdownItem
                  key="logout"
                  className="text-[#f0b0a4]"
                  color="danger"
                  onPress={handleLogout}
                >
                  {intl.formatMessage({ id: 'portal.account.logout' })}
                </DropdownItem>
              </DropdownSection>
            </DropdownMenu>
          </Dropdown>
        </div>
      </div>
    </header>
  )
}
