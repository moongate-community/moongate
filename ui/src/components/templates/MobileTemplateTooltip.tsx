import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'

import { Tooltip, TooltipContent, TooltipTrigger } from '../ui/tooltip'
import type { MobileTemplateSummary } from '../../lib/templates'

/** A template's own name, or its id — many draw their name from a pool and carry none. */
export const mobileTemplateLabel = (template: MobileTemplateSummary) =>
  template.name === '' ? template.id : template.name

/**
 * A hover card for a spawn template, in the same shape as the item one. It renders from the row it
 * was handed and **fetches nothing**: everything here is already on screen.
 */
export function MobileTemplateTooltip({
  template,
  children,
}: {
  template: MobileTemplateSummary
  children: ReactNode
}) {
  const { t } = useTranslation()
  const name = mobileTemplateLabel(template)

  return (
    <Tooltip>
      <TooltipTrigger asChild>{children}</TooltipTrigger>

      <TooltipContent>
        <div className="flex flex-col gap-2">
          <div className="flex items-start gap-2">
            <img src={template.imageUrl} alt={name} className="h-12 w-10 shrink-0 object-contain" />

            <span className="min-w-0">
              <span className="block truncate font-bold text-ink">{name}</span>
              {template.title !== '' && <span className="block truncate text-xs text-muted">{template.title}</span>}
            </span>
          </div>

          <dl className="grid grid-cols-[auto_1fr] gap-x-4 border-t border-ink/10 pt-2 text-xs">
            <Row
              label={t('admin.templates.stats')}
              value={`${template.strength} / ${template.dexterity} / ${template.intelligence}`}
            />
            <Row label={t('admin.templates.body')} value={template.body} />
            <Row label={t('admin.templates.category')} value={template.category} />
            {template.variantCount > 0 && <Row label={t('admin.templates.variants')} value={template.variantCount} />}
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
