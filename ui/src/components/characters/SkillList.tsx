import { useTranslation } from 'react-i18next'

import type { CharacterSkill } from '../../lib/characters'

/**
 * A character's trained skills. The list is what the server reports — sparse and alphabetical, since
 * a mobile never stores a skill left at zero — so there are no rows of noughts to scroll past.
 */
export function SkillList({ skills }: { skills: CharacterSkill[] }) {
  const { t } = useTranslation()

  if (skills.length === 0) {
    return <p className="text-sm text-muted">{t('characters.skills.empty')}</p>
  }

  const total = skills.reduce((sum, skill) => sum + skill.value, 0)

  return (
    <div className="flex flex-col gap-2">
      <ul className="flex flex-col">
        {skills.map((skill) => (
          <li key={skill.id} className="flex items-baseline justify-between gap-3 border-b border-ink/5 py-1 text-sm">
            <span className="min-w-0 truncate text-ink">{skill.name}</span>

            <span className="flex shrink-0 items-baseline gap-3">
              {/*
                Falls back to the server's own value: the arrow set could gain a member the portal
                has no word for, and a raw translation key is worse than an English one.
              */}
              <span className="text-xs text-muted">
                {t(`characters.skills.lock${skill.lock}`, { defaultValue: skill.lock })}
              </span>
              <span className="font-bold text-ink tabular-nums">{points(skill.value)}</span>
              <span className="w-10 text-right text-xs text-muted tabular-nums">{points(skill.cap)}</span>
            </span>
          </li>
        ))}
      </ul>

      <p className="text-right text-xs text-muted">{t('characters.skills.total', { total: points(total) })}</p>
    </div>
  )
}

/**
 * The server reports tenths of a point, so 62.5 is a real value rather than a rounding artefact — but
 * a whole number reads better as 50 than as 50.0.
 */
function points(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(1)
}
