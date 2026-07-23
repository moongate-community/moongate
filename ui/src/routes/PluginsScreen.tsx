import { useTranslation } from 'react-i18next'
import { Card } from '../components/ui/card'
import { Badge } from '../components/ui/badge'
import { useAdminPlugins } from '../lib/queries'

// The activated plugins, each with its version, declared route count, and an external/host badge — moved
// out of the overview into its own admin page.
export function PluginsScreen() {
  const { t } = useTranslation()
  const plugins = useAdminPlugins()

  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-xl text-ink">{t('admin.plugins.title')}</h1>

      <Card className="p-5">
        {plugins.isError && (
          <p role="alert" className="text-sm text-danger-text">
            {t('error.generic')}
          </p>
        )}
        {plugins.data?.length === 0 && <p className="text-sm text-muted">{t('admin.plugins.empty')}</p>}
        <ul className="flex flex-col gap-2">
          {plugins.data?.map((plugin) => (
            <li
              key={plugin.id}
              className="flex items-center gap-3 rounded-card border border-border-subtle bg-page px-3 py-2.5"
            >
              <span className="text-sm font-bold text-ink">{plugin.name}</span>
              <span className="font-mono text-xs text-muted">{plugin.version}</span>
              <span className="text-xs text-faint">{t('admin.plugins.routes', { count: plugin.routes.length })}</span>
              <span className="ml-auto">
                <Badge variant={plugin.isExternal ? 'info' : 'staff'}>
                  {plugin.isExternal ? t('admin.plugins.external') : t('admin.plugins.host')}
                </Badge>
              </span>
            </li>
          ))}
        </ul>
      </Card>
    </div>
  )
}
