import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { CATALOGUES, useCatalogue, type Catalogue, type CatalogueEntry } from '@/lib/catalogues'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from './dialog'
import { FilterPill } from './filter-pill'
import { Input } from './input'
import { Button } from './button'
import { RarityTile } from './rarity-tile'

/**
 * One picker for every catalogue the shard has. Which catalogue is a choice inside the dialog rather
 * than a different component per entity: the six differ only in where they are read from and how a row
 * reads, and both of those are data — see CATALOGUES.
 *
 * It hands back the picked entry and nothing else. What that means is the caller's business: a value
 * to copy, a page to open, an item to give someone.
 */
export function EntityPicker({
  open,
  onOpenChange,
  onSelect,
  catalogues = CATALOGUES,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  onSelect: (entry: CatalogueEntry, catalogue: Catalogue) => void
  /** Narrows the picker to some of the catalogues. All of them by default. */
  catalogues?: Catalogue[]
}) {
  const { t } = useTranslation()
  const [chosen, setChosen] = useState(catalogues[0])
  const [search, setSearch] = useState('')
  const [filter, setFilter] = useState('')
  const [page, setPage] = useState(1)

  // A search typed for one catalogue means nothing in the next, and neither does its page.
  useEffect(() => {
    setSearch('')
    setFilter('')
    setPage(1)
  }, [chosen])

  const rows = useCatalogue(chosen, { page, search, filter })

  // Only the page that came from the catalogue on screen. React Query holds the previous one while
  // the next loads, and rows of one catalogue read through the reader of another are nonsense.
  const shown = rows.data?.catalogueId === chosen.id ? rows.data : undefined
  const entries = (shown?.items ?? []).map((row) => chosen.toEntry(row as never))
  const totalPages = shown?.totalPages ?? 1

  function pick(entry: CatalogueEntry) {
    onSelect(entry, chosen)
    onOpenChange(false)
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      {/* Three fifths of the viewport on a desktop, near-full on a phone where three fifths is a
          column too narrow to put a tile and two lines of text beside each other. */}
      <DialogContent className="flex max-h-[80vh] w-[92vw] max-w-none flex-col gap-4 md:w-[60vw]">
        <DialogHeader>
          <DialogTitle>{t('picker.title')}</DialogTitle>
        </DialogHeader>

        <div className="flex flex-wrap gap-1.5">
          {catalogues.map((catalogue) => (
            <FilterPill key={catalogue.id} active={catalogue.id === chosen.id} onClick={() => setChosen(catalogue)}>
              {t(`picker.catalogues.${catalogue.id}`)}
            </FilterPill>
          ))}
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <Input
            autoFocus
            value={search}
            onChange={(event) => {
              setSearch(event.target.value)
              setPage(1)
            }}
            placeholder={t(`picker.search.${chosen.id}`)}
            aria-label={t('picker.searchLabel')}
            className="min-w-48 flex-1"
          />

          {chosen.filter && (
            <select
              value={filter}
              onChange={(event) => {
                setFilter(event.target.value)
                setPage(1)
              }}
              aria-label={t('picker.filterLabel')}
              className="h-9 rounded-control border border-border-subtle bg-deep px-2 text-sm text-ink"
            >
              {chosen.filter.values.map((value) => (
                <option key={value} value={value}>
                  {value === '' ? t('picker.anyFlag') : value}
                </option>
              ))}
            </select>
          )}
        </div>

        <div className="min-h-40 flex-1 overflow-y-auto">
          {rows.isError ? (
            <p className="p-6 text-center text-sm text-danger-text">{t('error.generic')}</p>
          ) : entries.length === 0 ? (
            <p className="p-6 text-center text-sm text-faint">
              {rows.isPending || shown === undefined ? t('common.loading') : t('picker.empty')}
            </p>
          ) : (
            <ul className="grid grid-cols-[repeat(auto-fill,minmax(13rem,1fr))] gap-1.5">
              {entries.map((entry) => (
                <li key={entry.value}>
                  <button
                    type="button"
                    onClick={() => pick(entry)}
                    className="flex w-full items-center gap-2 rounded-control border border-transparent p-1.5 text-left hover:border-gold/40 hover:bg-gold/5"
                  >
                    {entry.swatch ? (
                      <span
                        aria-hidden="true"
                        className="inline-block size-[46px] shrink-0 rounded-control border border-border-subtle"
                        style={{ background: entry.swatch }}
                      />
                    ) : (
                      <RarityTile src={entry.imageUrl} alt={entry.label} />
                    )}

                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm text-ink">{entry.label}</span>
                      <span className="block truncate font-mono text-xs text-faint">{entry.detail}</span>
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className="flex items-center justify-between gap-2 border-t border-border-subtle pt-3">
          <span className="font-mono text-xs text-faint">{t('picker.count', { count: shown?.total ?? 0 })}</span>

          <span className="flex items-center gap-2">
            <Button
              type="button"
              variant="ghost"
              disabled={page <= 1}
              onClick={() => setPage((current) => Math.max(current - 1, 1))}
            >
              {t('common.previous')}
            </Button>
            <span className="font-mono text-xs text-muted">
              {page} / {Math.max(totalPages, 1)}
            </span>
            <Button
              type="button"
              variant="ghost"
              disabled={page >= totalPages}
              onClick={() => setPage((current) => current + 1)}
            >
              {t('common.next')}
            </Button>
          </span>
        </div>
      </DialogContent>
    </Dialog>
  )
}
