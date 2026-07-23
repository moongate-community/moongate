import { NavLink, Outlet } from 'react-router'
import { useTranslation } from 'react-i18next'

const linkClass = ({ isActive }: { isActive: boolean }) =>
  isActive
    ? 'flex items-center border-b-2 border-gold py-2 text-sm font-bold text-gold'
    : 'flex items-center border-b-2 border-transparent py-2 text-sm text-muted hover:text-ink'

/** The admin area's own tab row, above whichever admin screen is routed below it. */
export function AdminLayout() {
  const { t } = useTranslation()

  return (
    <div className="flex flex-col gap-5">
      <nav className="flex gap-6 border-b border-border-subtle">
        <NavLink to="/admin" end className={linkClass}>
          {t('admin.nav.overview')}
        </NavLink>
        <NavLink to="/admin/accounts" className={linkClass}>
          {t('admin.nav.accounts')}
        </NavLink>
        <NavLink to="/admin/plugins" className={linkClass}>
          {t('admin.nav.plugins')}
        </NavLink>
        <NavLink to="/admin/settings" className={linkClass}>
          {t('admin.nav.settings')}
        </NavLink>
        <NavLink to="/admin/console" className={linkClass}>
          {t('admin.nav.console')}
        </NavLink>
      </nav>
      <Outlet />
    </div>
  )
}
