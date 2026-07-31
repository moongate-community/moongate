import { useState } from 'react'
import { useTranslation } from 'react-i18next'

import { Card } from '../components/ui/card'
import { cn } from '../lib/utils'
import { figureUrl, paperdollUrl, useMyCharacters, type Character } from '../lib/characters'

export function CharactersScreen() {
  const { t } = useTranslation()
  const characters = useMyCharacters()

  // The chosen serial rather than the character: resolving it against the current list means a
  // character that disappears cannot leave a selection pointing at nothing, and arriving needs no
  // effect to pick the first one.
  const [chosen, setChosen] = useState<string | null>(null)

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

  const selected = all.find((one) => one.serial === chosen) ?? all[0]

  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-bold text-ink">{t('characters.title')}</h1>

      <div className="grid gap-6 lg:grid-cols-[16rem_1fr]">
        <Card className="gap-0 p-2">
          {all.map((one) => (
            <button
              key={one.serial}
              type="button"
              onClick={() => setChosen(one.serial)}
              className={cn(
                'flex items-center gap-3 rounded-lg px-3 py-2 text-left transition-colors',
                one.serial === selected.serial ? 'bg-gold/10 text-gold' : 'text-ink hover:bg-ink/5',
              )}
            >
              <CharacterImage
                src={figureUrl(one.serial)}
                alt={t('characters.figureAlt', { name: one.name })}
                className="h-12 w-10 object-contain"
                fallback={<span className="h-12 w-10" aria-hidden="true" />}
              />
              <span className="min-w-0">
                <span className="block truncate font-bold">{one.name}</span>
                <span className="block truncate text-xs text-muted">{one.race}</span>
              </span>
            </button>
          ))}
        </Card>

        <CharacterPanel character={selected} />
      </div>
    </section>
  )
}

function CharacterPanel({ character }: { character: Character }) {
  const { t } = useTranslation()

  return (
    <Card className="items-center gap-4 py-8">
      <header className="text-center">
        <h2 className="text-xl font-bold text-ink">{character.name}</h2>
        <p className="text-sm text-muted">
          {t('characters.identity', { race: character.race, gender: character.gender })}
        </p>
      </header>

      {/* A fixed box so the panel does not jump while the picture loads. */}
      <div className="flex h-[260px] w-[210px] items-center justify-center">
        <CharacterImage
          // Keyed by serial so switching characters remounts the image: without it a picture that
          // failed once would keep its placeholder for the next character too.
          key={character.serial}
          src={paperdollUrl(character.serial)}
          alt={t('characters.paperdollAlt', { name: character.name })}
          className="max-h-full max-w-full object-contain"
          fallback={<p className="text-center text-sm text-muted">{t('characters.noImage')}</p>}
        />
      </div>

      <dl className="grid w-full max-w-sm grid-cols-2 gap-x-6 gap-y-2 px-6 text-sm">
        <Stat label={t('characters.strength')} value={character.strength} />
        <Stat label={t('characters.dexterity')} value={character.dexterity} />
        <Stat label={t('characters.intelligence')} value={character.intelligence} />
        <Stat label={t('characters.kills')} value={character.kills} />
        <Stat
          label={t('characters.hits')}
          value={t('characters.pool', { current: character.hits, max: character.hitsMax })}
        />
        <Stat
          label={t('characters.stamina')}
          value={t('characters.pool', { current: character.stamina, max: character.staminaMax })}
        />
        <Stat
          label={t('characters.mana')}
          value={t('characters.pool', { current: character.mana, max: character.manaMax })}
        />
        <Stat label={t('characters.location')} value={`${character.x}, ${character.y}`} />
      </dl>
    </Card>
  )
}

function Stat({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="flex justify-between gap-2">
      <dt className="text-muted">{label}</dt>
      <dd className="font-bold text-ink">{value}</dd>
    </div>
  )
}

/**
 * An image that gives way to something readable when it fails. A character whose body has no
 * animation legitimately 404s from the image route, and the browser's broken-image icon is not an
 * answer for a page whose whole point is the picture.
 */
function CharacterImage({
  src,
  alt,
  className,
  fallback,
}: {
  src: string
  alt: string
  className?: string
  fallback: React.ReactNode
}) {
  const [failed, setFailed] = useState(false)

  if (failed) {
    return <>{fallback}</>
  }

  return <img src={src} alt={alt} className={className} onError={() => setFailed(true)} />
}
