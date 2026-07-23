import { useTranslation } from 'react-i18next'
import { StatCard } from '../components/ui/stat-card'
import { useAdminStatus, useStats } from '../lib/queries'

// The admin overview: read-only shard status and statistics as rows of stat cards. The plugin list lives
// on its own admin page.
export function AdminScreen() {
  const { t } = useTranslation()
  const status = useAdminStatus()
  const stats = useStats()

  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-xl text-ink">{t('admin.title')}</h1>

      <section className="flex flex-col gap-3">
        <h2 className="font-display text-[16px] tracking-[0.08em] text-gold">{t('admin.status.title')}</h2>
        <div className="grid grid-cols-3 gap-4">
          <StatCard label={t('app.name')} value={status.data?.shardName} tone="text-gold" />
          <StatCard label={t('admin.status.build')} value={status.data?.version} />
          <StatCard label={t('admin.status.sessions')} value={status.data?.onlineSessions} />
        </div>
      </section>

      <section className="flex flex-col gap-3">
        <h2 className="font-display text-[16px] tracking-[0.08em] text-gold">{t('admin.stats.title')}</h2>
        <div className="grid grid-cols-4 gap-4">
          <StatCard label={t('admin.stats.players')} value={stats.data?.players.online} tone="text-success" />
          <StatCard label={t('admin.stats.accounts')} value={stats.data?.accounts.total} />
          <StatCard label={t('admin.stats.npcs')} value={stats.data?.world.npcs} />
          <StatCard label={t('admin.stats.items')} value={stats.data?.world.items} />
        </div>
      </section>
    </div>
  )
}
