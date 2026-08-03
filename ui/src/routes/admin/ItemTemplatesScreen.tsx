import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import type { ColumnDef } from '@tanstack/react-table'

import { DataTable } from '../../components/ui/data-table'
import { StatCard } from '../../components/ui/stat-card'
import { ScreenTitle } from '../../components/ui/section-title'
import { ItemTemplateTooltip } from '../../components/templates/ItemTemplateTooltip'
import { rarityColor } from '../../components/templates/RarityBadge'
import { useItemTemplates, type ItemTemplateSummary } from '../../lib/templates'

/** What to call a template the server could not name: its id beats an empty cell. */
const label = (template: ItemTemplateSummary) => (template.name === '' ? template.id : template.name)

export function ItemTemplatesScreen() {
  const { t } = useTranslation()
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const templates = useItemTemplates({ page, search })

  const columns = useMemo<ColumnDef<ItemTemplateSummary>[]>(
    () => [
      {
        id: 'preview',
        header: t('admin.templates.preview'),
        cell: ({ row }) => (
          <ItemTemplateTooltip template={row.original}>
            <span
              tabIndex={0}
              className="inline-block rounded outline-none focus-visible:ring-2 focus-visible:ring-gold"
            >
              <img src={row.original.imageUrl} alt={label(row.original)} className="h-16 w-16 object-contain" />
            </span>
          </ItemTemplateTooltip>
        ),
      },
      {
        accessorKey: 'name',
        header: t('admin.templates.name'),
        cell: ({ row }) => (
          <Link
            to={`/admin/items/${row.original.id}`}
            className="font-bold hover:underline"
            style={{ color: rarityColor(row.original.rarity) }}
          >
            {label(row.original)}
          </Link>
        ),
      },
      {
        accessorKey: 'id',
        header: t('admin.templates.id'),
        cell: ({ getValue }) => <span className="font-mono text-xs text-muted">{getValue() as string}</span>,
      },
      { accessorKey: 'category', header: t('admin.templates.category') },
      {
        accessorKey: 'weight',
        header: t('admin.templates.weight'),
        cell: ({ getValue }) => <span className="font-mono tabular-nums">{getValue() as number}</span>,
      },
      {
        accessorKey: 'goldValue',
        header: t('admin.templates.value'),
        cell: ({ getValue }) => <span className="font-mono tabular-nums text-gold">{getValue() as number}</span>,
      },
      {
        id: 'tags',
        header: t('admin.templates.tags'),
        cell: ({ row }) => row.original.tags.join(', '),
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

  if (templates.isError) {
    return (
      <p role="alert" className="text-sm text-danger-text">
        {t('admin.templates.loadFailed')}
      </p>
    )
  }

  const result = templates.data

  return (
    <div className="flex flex-col gap-4">
      <ScreenTitle>{t('admin.templates.itemsTitle')}</ScreenTitle>

      {/* The count is the fact this screen is about: 1665 templates is the shape of the catalogue. */}
      {result && (
        <div className="grid gap-4 sm:grid-cols-3">
          <StatCard
            label={t('admin.templates.itemsTitle')}
            value={result.total}
            sub={t('admin.templates.onThisShard')}
          />
          <StatCard label={t('admin.templates.page')} value={`${result.page} / ${Math.max(result.totalPages, 1)}`} />
          <StatCard label={t('admin.templates.showing')} value={result.items.length} tone="text-gold" />
        </div>
      )}

      <DataTable
        columns={columns}
        data={result?.items ?? []}
        searchPlaceholder={t('admin.templates.search')}
        search={{ value: search, onChange: changeSearch }}
        pagination={{ page, totalPages: result?.totalPages ?? 1, onPageChange: setPage }}
      />

      {result && result.items.length === 0 && <p className="text-sm text-muted">{t('admin.templates.empty')}</p>}
    </div>
  )
}
