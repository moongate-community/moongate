import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'

import { Tooltip, TooltipContent, TooltipTrigger } from '../ui/tooltip'
import { useItemTooltip } from '../../lib/characters'

/**
 * What the game says about an item, shown when you point at it.
 *
 * The request is made when the tooltip opens, never on render: a character's backpack is dozens of
 * rows, and fetching on mount would turn one page view into one request each. `open` drives the
 * hook's `enabled` for exactly that reason.
 */
export function ItemTooltip({
  characterSerial,
  itemSerial,
  children,
}: {
  characterSerial: string
  itemSerial: string
  children: ReactNode
}) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const tooltip = useItemTooltip(characterSerial, itemSerial, open)

  return (
    <Tooltip open={open} onOpenChange={setOpen}>
      <TooltipTrigger asChild>{children}</TooltipTrigger>

      <TooltipContent>
        {tooltip.data === undefined ? (
          <p className="text-muted">{t('characters.tooltip.loading')}</p>
        ) : (
          <div className="flex flex-col gap-2">
            {tooltip.data.lines.length === 0 ? (
              <p className="text-muted">{t('characters.tooltip.unavailable')}</p>
            ) : (
              <div className="flex flex-col">
                {tooltip.data.lines.map((line) => (
                  <span key={line}>{line}</span>
                ))}
              </div>
            )}

            <dl className="grid grid-cols-[auto_1fr] gap-x-3 border-t border-ink/10 pt-2 text-xs text-muted">
              <dt>{t('characters.tooltip.template')}</dt>
              <dd className="text-right">{tooltip.data.templateId}</dd>

              {tooltip.data.hue !== 0 && (
                <>
                  <dt>{t('characters.tooltip.hue')}</dt>
                  <dd className="text-right">{`0x${tooltip.data.hue.toString(16)}`}</dd>
                </>
              )}

              {tooltip.data.amount > 1 && (
                <>
                  <dt>{t('characters.tooltip.amount')}</dt>
                  <dd className="text-right">{tooltip.data.amount}</dd>
                </>
              )}

              <dt>{t('characters.tooltip.serial')}</dt>
              <dd className="text-right">{tooltip.data.serial}</dd>
            </dl>
          </div>
        )}
      </TooltipContent>
    </Tooltip>
  )
}
