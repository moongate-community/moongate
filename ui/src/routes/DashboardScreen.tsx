import { useTranslation } from 'react-i18next'
import { Card } from '../components/ui/card'
import { useMe, useMyCharacters, useStats } from '../lib/queries'

export function DashboardScreen() {
  const { t } = useTranslation()
  const me = useMe()
  const characters = useMyCharacters()
  const stats = useStats()

  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-xl text-ink">
        {t('dashboard.welcome', { name: me.data?.username ?? '' })}
      </h1>

      <Card className="p-5">
        <h2 className="mb-3 font-display text-[13px] tracking-widest text-gold">
          {t('dashboard.characters')}
        </h2>

        {characters.isPending && <p className="text-sm text-muted">{t('common.loading')}</p>}
        {characters.isError && (
          <p role="alert" className="text-sm text-danger-text">
            {t('error.generic')}
          </p>
        )}
        {characters.data?.length === 0 && (
          <p className="text-sm text-muted">{t('dashboard.noCharacters')}</p>
        )}

        <ul className="flex flex-col gap-2">
          {characters.data?.map((character) => (
            <li
              key={character.serial}
              className="flex items-center gap-3 rounded-card border border-border-subtle bg-page px-3 py-2.5"
            >
              <span className="text-sm font-bold text-ink">{character.name}</span>
              <span className="text-xs text-muted">{character.race}</span>
              <span className="ml-auto font-mono text-xs text-faint">{character.serial}</span>
            </li>
          ))}
        </ul>
      </Card>

      <Card className="p-5">
        <h2 className="mb-3 font-display text-[13px] tracking-widest text-gold">{t('dashboard.shard')}</h2>

        {stats.isError && (
          <p role="alert" className="text-sm text-danger-text">
            {t('error.generic')}
          </p>
        )}

        {/* Optional all the way down because the schema says so: Swashbuckle marks nothing required
            without SupportNonNullableReferenceTypes, so the generated types cannot promise these nested
            objects exist. Figure renders an em dash for a missing number, which is also what should show
            while the first snapshot is still being taken. */}
        <dl className="grid grid-cols-3 gap-4">
          <Figure label={t('dashboard.playersOnline')} value={stats.data?.players?.online} />
          <Figure label={t('dashboard.accounts')} value={stats.data?.accounts?.total} />
          <Figure label={t('dashboard.charactersTotal')} value={stats.data?.accounts?.characters} />
        </dl>
      </Card>
    </div>
  )
}

function Figure({ label, value }: { label: string; value: number | undefined }) {
  return (
    <div>
      <dt className="text-[11px] uppercase tracking-widest text-faint">{label}</dt>
      <dd className="font-mono text-2xl font-bold text-ink">{value ?? '—'}</dd>
    </div>
  )
}
