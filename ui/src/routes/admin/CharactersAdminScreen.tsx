import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'

import { DataTable } from '../../components/ui/data-table'
import { CharacterImage } from '../../components/characters/CharacterImage'
import { figureUrl, useAllCharacters, type Character } from '../../lib/characters'

export function CharactersAdminScreen() {
  const { t } = useTranslation()
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const characters = useAllCharacters({ page, search })

  const columns = useMemo<ColumnDef<Character>[]>(
    () => [
      {
        id: 'preview',
        header: t('admin.characters.preview'),
        cell: ({ row }) => (
          <CharacterImage
            src={figureUrl(row.original.serial)}
            alt={row.original.name}
            className="h-16 w-16 object-contain"
            fallback={<span className="block h-16 w-16 text-center text-muted">{t('admin.characters.noImage')}</span>}
          />
        ),
      },
      { accessorKey: 'name', header: t('admin.characters.name') },
      {
        accessorKey: 'accountUsername',
        header: t('admin.characters.account'),
        cell: ({ getValue }) => (getValue() as string | null) ?? t('admin.characters.unknownAccount'),
      },
      {
        accessorKey: 'race',
        header: t('admin.characters.race'),
        cell: ({ row }) => t(`characters.race.${row.original.race}`, { defaultValue: row.original.race }),
      },
      {
        id: 'stats',
        header: t('admin.characters.stats'),
        cell: ({ row }) => `${row.original.strength} / ${row.original.dexterity} / ${row.original.intelligence}`,
      },
      {
        id: 'hits',
        header: t('admin.characters.hits'),
        cell: ({ row }) => `${row.original.hits} / ${row.original.hitsMax}`,
      },
      {
        id: 'location',
        header: t('admin.characters.location'),
        cell: ({ row }) => `${row.original.x}, ${row.original.y}`,
      },
    ],
    [t],
  )

  // A new search restarts the paging. Without this, searching from page 3 asks the server for page 3
  // of a result that may have one page, and the empty table reads as "no matches" for a search that
  // in fact matched.
  function changeSearch(value: string) {
    setSearch(value)
    setPage(1)
  }

  if (characters.isError) {
    return (
      <p role="alert" className="text-sm text-danger-text">
        {t('admin.characters.loadFailed')}
      </p>
    )
  }

  const result = characters.data

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="font-display text-xl text-ink">{t('admin.characters.title')}</h1>
        {result && <span className="text-sm text-muted">{t('admin.characters.total', { count: result.total })}</span>}
      </div>

      <DataTable
        columns={columns}
        data={result?.items ?? []}
        searchPlaceholder={t('admin.characters.search')}
        search={{ value: search, onChange: changeSearch }}
        pagination={{ page, totalPages: result?.totalPages ?? 1, onPageChange: setPage }}
      />

      {result && result.items.length === 0 && <p className="text-sm text-muted">{t('admin.characters.empty')}</p>}
    </div>
  )
}
