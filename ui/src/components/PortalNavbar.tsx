import { Avatar, Dropdown, DropdownItem, DropdownMenu, DropdownSection, DropdownTrigger } from '@heroui/react'
import { useIntl } from 'react-intl'
import { NavLink, useNavigate } from 'react-router-dom'
import { PortalLanguageSwitcher } from './PortalLanguageSwitcher'
import { usePortalAuthStore } from '../store/portalAuthStore'

function PortalNavItem({ to, label }: { to: string, label: string }) {
  return (
    <NavLink
      to={to}
      className="rounded-md px-3 py-2 font-mono text-xs uppercase tracking-[0.18em] transition-colors"
      style={({ isActive }) => ({
        color: isActive ? 'var(--mg-text)' : 'color-mix(in srgb, var(--mg-text) 68%, transparent)',
        background: isActive ? 'color-mix(in srgb, var(--mg-accent) 14%, transparent)' : 'transparent',
        border: isActive ? '1px solid color-mix(in srgb, var(--mg-accent) 26%, transparent)' : '1px solid transparent',
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
        background: 'linear-gradient(180deg, color-mix(in srgb, var(--mg-panel) 92%, var(--mg-bg) 8%), color-mix(in srgb, var(--mg-panel-soft) 88%, var(--mg-bg) 12%))',
        borderColor: 'var(--mg-border)',
      }}
    >
      <div className="mx-auto flex w-full max-w-[1180px] items-center justify-between gap-4">
        <div className="flex items-center gap-2">
          <PortalNavItem to="/portal/account" label={intl.formatMessage({ id: 'portal.nav.characters' })} />
        </div>

        <div className="flex items-center gap-3">
          <PortalLanguageSwitcher />
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
                    background: 'linear-gradient(180deg, var(--mg-accent) 0%, var(--mg-accent-2) 100%)',
                    color: 'color-mix(in srgb, var(--mg-bg) 82%, black 18%)',
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
                background: 'linear-gradient(180deg, color-mix(in srgb, var(--mg-panel) 96%, var(--mg-bg) 4%), color-mix(in srgb, var(--mg-panel-soft) 94%, var(--mg-bg) 6%))',
                border: '1px solid var(--mg-border)',
              }}
            >
              <DropdownSection
                title={user?.username ?? intl.formatMessage({ id: 'portal.account.unknown' })}
                classNames={{
                  heading: 'font-mono text-[11px] uppercase tracking-[0.18em] text-[color:color-mix(in_srgb,var(--mg-text)_50%,transparent)]',
                }}
              >
                <DropdownItem
                  key="profile"
                  className="text-[color:var(--mg-text)]"
                  onPress={() => navigate('/portal/profile')}
                >
                  {intl.formatMessage({ id: 'portal.nav.profile' })}
                </DropdownItem>
                <DropdownItem
                  key="logout"
                  className="text-[color:#f0b0a4]"
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
