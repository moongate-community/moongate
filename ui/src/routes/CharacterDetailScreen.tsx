import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'

import { Card } from '../components/ui/card'
import { SectionTitle } from '../components/ui/section-title'
import { CharacterImage } from '../components/characters/CharacterImage'
import { InventoryTree } from '../components/characters/InventoryTree'
import { SkillList } from '../components/characters/SkillList'
import { ApiError } from '../lib/api'
import { paperdollUrl, useCharacter, type CharacterDetail } from '../lib/characters'

export function CharacterDetailScreen() {
  const { t } = useTranslation()
  const { serial = '' } = useParams()
  const character = useCharacter(serial)

  if (character.isError) {
    return (
      <p role="alert" className="text-sm text-danger-text">
        {t(failureKey(character.error))}
      </p>
    )
  }

  if (character.data === undefined) {
    return <p className="text-sm text-muted">{t('common.loading')}</p>
  }

  return <Detail detail={character.data} />
}

/**
 * Which failure to name. "Not yours" and "does not exist" are different answers to the reader, and
 * collapsing them into one message would leave a player wondering which they hit.
 */
function failureKey(error: unknown): string {
  if (error instanceof ApiError && error.status === 403) {
    return 'characters.detail.forbidden'
  }

  if (error instanceof ApiError && error.status === 404) {
    return 'characters.detail.notFound'
  }

  return 'characters.detail.loadFailed'
}

function Detail({ detail }: { detail: CharacterDetail }) {
  const { t } = useTranslation()
  const character = detail.character

  return (
    <section className="space-y-4">
      <Link to="/characters" className="text-sm text-muted hover:text-ink">
        {t('characters.detail.back')}
      </Link>

      <div className="grid gap-6 lg:grid-cols-[20rem_1fr]">
        <Card className="items-center gap-4 py-8">
          <header className="text-center">
            <h1 className="text-xl font-bold text-ink">{character.name}</h1>
            <p className="text-sm text-muted">
              {t('characters.identity', {
                race: t(`characters.race.${character.race}`, { defaultValue: character.race }),
                gender: t(`characters.gender.${character.gender}`, { defaultValue: character.gender }),
              })}
            </p>
          </header>

          {/* A fixed box so the panel does not jump while the picture loads. */}
          <div className="flex h-[260px] w-[210px] items-center justify-center">
            <CharacterImage
              src={paperdollUrl(character.serial)}
              alt={t('characters.paperdollAlt', { name: character.name })}
              className="max-h-full max-w-full object-contain"
              fallback={<p className="text-center text-sm text-muted">{t('characters.noImage')}</p>}
            />
          </div>

          <dl className="grid w-full grid-cols-2 gap-x-6 gap-y-2 px-6 text-sm">
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

        <div className="flex flex-col gap-6">
          <Card className="gap-3 p-4">
            <SectionTitle>{t('characters.detail.equipment')}</SectionTitle>
            <InventoryTree
              items={detail.equipment}
              emptyLabel={t('characters.detail.equipmentEmpty')}
              characterSerial={character.serial}
            />
          </Card>

          <Card className="gap-3 p-4">
            <SectionTitle>{t('characters.skills.title')}</SectionTitle>
            <SkillList skills={detail.skills} />
          </Card>

          <Card className="gap-3 p-4">
            <SectionTitle>{t('characters.detail.backpack')}</SectionTitle>
            <InventoryTree
              items={detail.backpack}
              emptyLabel={t('characters.detail.backpackEmpty')}
              characterSerial={character.serial}
            />
          </Card>
        </div>
      </div>
    </section>
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
