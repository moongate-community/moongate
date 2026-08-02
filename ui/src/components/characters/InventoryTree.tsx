import { useTranslation } from 'react-i18next'

import { CharacterImage } from './CharacterImage'
import { itemImageUrl, type CharacterItem } from '../../lib/characters'

/**
 * A character's items as a nested list rather than a table: the data is a tree, and a flat table
 * cannot say what is inside what. Each row carries the item's art, so an item named only by cliloc —
 * which is most of them — is still recognisable.
 */
export function InventoryTree({ items, emptyLabel }: { items: CharacterItem[]; emptyLabel: string }) {
  const { t } = useTranslation()

  if (items.length === 0) {
    return <p className="text-sm text-muted">{emptyLabel}</p>
  }

  return (
    <ul className="flex flex-col gap-1">
      {items.map((item) => (
        <li key={item.serial}>
          <div className="flex items-center gap-3 rounded-lg px-2 py-1">
            <CharacterImage
              src={itemImageUrl(item.itemId, item.hue)}
              alt={item.name === '' ? t('characters.inventory.unnamed') : item.name}
              className="h-16 w-16 shrink-0 object-contain"
              fallback={
                <span className="flex h-16 w-16 shrink-0 items-center justify-center text-muted">
                  {t('characters.inventory.noImage')}
                </span>
              }
            />

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
              <InventoryTree items={item.contents} emptyLabel={emptyLabel} />
            </div>
          )}
        </li>
      ))}
    </ul>
  )
}
