import { useTranslation } from 'react-i18next'
import { Card } from '../components/ui/card'
import { StatCard } from '../components/ui/stat-card'
import { Badge } from '../components/ui/badge'
import { useAdminPlugins, useAdminStatus, useStats } from '../lib/queries'

// The admin area's first screen: read-only diagnostics, composed from the Moongate control kit — a row of
// stat cards over the shard status and statistics, and a card listing the active plugins.
export function AdminScreen() {
  const { t } = useTranslation()
  const status = useAdminStatus()
  const stats = useStats()
  const plugins = useAdminPlugins()

  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-xl text-ink">{t('admin.title')}</h1>

      <section className="flex flex-col gap-3">
        <h2 className="font-display text-[14px] tracking-[0.08em] text-gold">{t('admin.status.title')}</h2>
        <div className="grid grid-cols-3 gap-4">
          <StatCard label={t('app.name')} value={status.data?.shardName} tone="text-gold" />
          <StatCard label={t('admin.status.build')} value={status.data?.version} />
          <StatCard label={t('admin.status.sessions')} value={status.data?.onlineSessions} />
        </div>
      </section>

      <section className="flex flex-col gap-3">
        <h2 className="font-display text-[14px] tracking-[0.08em] text-gold">{t('admin.stats.title')}</h2>
        <div className="grid grid-cols-4 gap-4">
          <StatCard label={t('admin.stats.players')} value={stats.data?.players.online} tone="text-success" />
          <StatCard label={t('admin.stats.accounts')} value={stats.data?.accounts.total} />
          <StatCard label={t('admin.stats.npcs')} value={stats.data?.world.npcs} />
          <StatCard label={t('admin.stats.items')} value={stats.data?.world.items} />
        </div>
      </section>

      <Card className="p-5">
        <h2 className="mb-3 font-display text-[14px] tracking-[0.08em] text-gold">{t('admin.plugins.title')}</h2>
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
