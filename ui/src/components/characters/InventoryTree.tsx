import { useTranslation } from 'react-i18next'

import { RarityTile } from '../ui/rarity-tile'
import { ItemTooltip } from './ItemTooltip'
import { itemImageUrl, type CharacterItem } from '../../lib/characters'

/**
 * A character's items as a nested list rather than a table: the data is a tree, and a flat table
 * cannot say what is inside what. Each row carries the item's art, so an item named only by cliloc —
 * which is most of them — is still recognisable.
 */
export function InventoryTree({
  items,
  emptyLabel,
  characterSerial,
}: {
  items: CharacterItem[]
  emptyLabel: string
  characterSerial: string
}) {
  const { t } = useTranslation()

  if (items.length === 0) {
    return <p className="text-sm text-muted">{emptyLabel}</p>
  }

  return (
    <ul className="flex flex-col gap-1">
      {items.map((item) => (
        <li key={item.serial}>
          <div className="flex items-center gap-3 rounded-lg px-2 py-1">
            {/*
              The trigger is the picture, not the row: anchoring to a full-width row would put the
              tooltip somewhere in the middle of it rather than beside what you are pointing at.
            */}
            <ItemTooltip characterSerial={characterSerial} itemSerial={item.serial}>
              <span tabIndex={0} className="shrink-0 rounded outline-none focus-visible:ring-2 focus-visible:ring-gold">
                <RarityTile
                  src={itemImageUrl(item.itemId, item.hue)}
                  alt={item.name === '' ? t('characters.inventory.unnamed') : item.name}
                  size={64}
                />
              </span>
            </ItemTooltip>

            <span className="min-w-0">
              <span className="block truncate text-ink">
                {item.name === '' ? t('characters.inventory.unnamed') : item.name}
              </span>
              <span className="flex gap-2 text-xs text-muted">
                {item.amount > 1 && <span>{t('characters.inventory.amount', { count: item.amount })}</span>}
                {item.layer !== null && <span>{item.layer}</span>}
              </span>
            </span>
          </div>

          {item.contents.length > 0 && (
            <div className="ml-8 border-l border-ink/10 pl-2">
              <InventoryTree items={item.contents} emptyLabel={emptyLabel} characterSerial={characterSerial} />
            </div>
          )}
        </li>
      ))}
    </ul>
  )
}
