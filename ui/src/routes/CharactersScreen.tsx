import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'

import { Card } from '../components/ui/card'
import { CharacterImage } from '../components/characters/CharacterImage'
import { figureUrl, useMyCharacters } from '../lib/characters'

export function CharactersScreen() {
  const { t } = useTranslation()
  const characters = useMyCharacters()

  if (characters.isPending) {
    return <p className="text-sm text-muted">{t('common.loading')}</p>
  }

  if (characters.isError) {
    return (
      <p role="alert" className="text-sm text-danger-text">
        {t('characters.loadFailed')}
      </p>
    )
  }

  const all = characters.data ?? []

  if (all.length === 0) {
    return (
      <section className="space-y-4">
        <h1 className="text-2xl font-bold text-ink">{t('characters.title')}</h1>
        <Card className="items-center gap-2 py-12 text-center">
          <p className="font-bold text-ink">{t('characters.empty')}</p>
          <p className="text-sm text-muted">{t('characters.emptyHint')}</p>
        </Card>
      </section>
    )
  }

  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-bold text-ink">{t('characters.title')}</h1>

      <Card className="gap-0 p-2">
        {all.map((one) => (
          <Link
            key={one.serial}
            to={`/characters/${one.serial}`}
            className="flex items-center gap-3 rounded-lg px-3 py-2 text-ink transition-colors hover:bg-ink/5"
          >
            <CharacterImage
              src={figureUrl(one.serial)}
              alt={t('characters.figureAlt', { name: one.name })}
              className="h-12 w-10 object-contain"
              fallback={<span className="h-12 w-10" aria-hidden="true" />}
            />
            <span className="min-w-0">
              <span className="block truncate font-bold">{one.name}</span>
              <span className="block truncate text-xs text-muted">
                {t(`characters.race.${one.race}`, { defaultValue: one.race })}
              </span>
            </span>
          </Link>
        ))}
      </Card>
    </section>
  )
}
