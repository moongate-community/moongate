import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'

import { Tooltip, TooltipContent, TooltipTrigger } from '../ui/tooltip'
import { RarityBadge, rarityColor } from './RarityBadge'
import type { ItemTemplateSummary } from '../../lib/templates'

/**
 * A hover card for an item template, in the shape players know from Wowhead: the sprite beside a
 * rarity-coloured name, then the numbers.
 *
 * It renders from the row it was handed and **fetches nothing** — unlike the item tooltip, whose
 * property list lives on the game loop and is not in the row. Everything here is already on screen,
 * so a request per hover would buy nothing.
 */
export function ItemTemplateTooltip({ template, children }: { template: ItemTemplateSummary; children: ReactNode }) {
  const { t } = useTranslation()
  const name = template.name === '' ? template.id : template.name

  return (
    <Tooltip>
      <TooltipTrigger asChild>{children}</TooltipTrigger>

      <TooltipContent>
        <div className="flex flex-col gap-2">
          <div className="flex items-start gap-2">
            <img src={template.imageUrl} alt={name} className="h-10 w-10 shrink-0 object-contain" />

            <span className="min-w-0">
              <span className="block truncate font-bold" style={{ color: rarityColor(template.rarity) }}>
                {name}
              </span>
              <RarityBadge rarity={template.rarity} />
            </span>
          </div>

          <dl className="grid grid-cols-[auto_1fr] gap-x-4 border-t border-ink/10 pt-2 text-xs">
            <Row label={t('admin.templates.weight')} value={template.weight} />
            <Row label={t('admin.templates.value')} value={template.goldValue} />
            <Row label={t('admin.templates.category')} value={template.category} />
            {template.tags.length > 0 && <Row label={t('admin.templates.tags')} value={template.tags.join(', ')} />}
          </dl>
        </div>
      </TooltipContent>
    </Tooltip>
  )
}

function Row({ label, value }: { label: string; value: string | number }) {
  return (
    <>
      <dt className="text-muted">{label}</dt>
      <dd className="text-right text-ink">{value}</dd>
    </>
  )
}
