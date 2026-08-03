import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import type { ColumnDef } from '@tanstack/react-table'

import { DataTable } from '../../components/ui/data-table'
import { StatCard } from '../../components/ui/stat-card'
import { ScreenTitle } from '../../components/ui/section-title'
import { MobileTemplateTooltip, mobileTemplateLabel } from '../../components/templates/MobileTemplateTooltip'
import { useMobileTemplates, type MobileTemplateSummary } from '../../lib/templates'

export function MobileTemplatesScreen() {
  const { t } = useTranslation()
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const templates = useMobileTemplates({ page, search })

  const columns = useMemo<ColumnDef<MobileTemplateSummary>[]>(
    () => [
      {
        id: 'preview',
        header: t('admin.templates.preview'),
        cell: ({ row }) => (
          <MobileTemplateTooltip template={row.original}>
            <span
              tabIndex={0}
              className="inline-block rounded outline-none focus-visible:ring-2 focus-visible:ring-gold"
            >
              <img
                src={row.original.imageUrl}
                alt={mobileTemplateLabel(row.original)}
                className="h-16 w-12 object-contain"
              />
            </span>
          </MobileTemplateTooltip>
        ),
      },
      {
        accessorKey: 'name',
        header: t('admin.templates.name'),
        cell: ({ row }) => (
          <Link to={`/admin/mobiles/${row.original.id}`} className="font-bold text-gold hover:underline">
            {mobileTemplateLabel(row.original)}
          </Link>
        ),
      },
      { accessorKey: 'title', header: t('admin.templates.title') },
      { accessorKey: 'category', header: t('admin.templates.category') },
      {
        accessorKey: 'body',
        header: t('admin.templates.body'),
        cell: ({ getValue }) => <span className="font-mono tabular-nums text-muted">{getValue() as number}</span>,
      },
      {
        id: 'stats',
        header: t('admin.templates.stats'),
        cell: ({ row }) => (
          <span className="font-mono tabular-nums">
            {`${row.original.strength} / ${row.original.dexterity} / ${row.original.intelligence}`}
          </span>
        ),
      },
      {
        accessorKey: 'variantCount',
        header: t('admin.templates.variants'),
        cell: ({ getValue }) => <span className="font-mono tabular-nums">{getValue() as number}</span>,
      },
    ],
    [t],
  )

  // A new search restarts the paging, so searching from page 3 cannot land on page 3 of a one-page
  // result and read as "no matches" for a search that matched.
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
      <ScreenTitle>{t('admin.templates.mobilesTitle')}</ScreenTitle>

      {result && (
        <div className="grid gap-4 sm:grid-cols-3">
          <StatCard
            label={t('admin.templates.mobilesTitle')}
            value={result.total}
            sub={t('admin.templates.onThisShard')}
          />
          <StatCard label={t('admin.templates.page')} value={`${result.page} / ${Math.max(result.totalPages, 1)}`} />
          <StatCard
            label={t('admin.templates.withVariants')}
            value={result.items.filter((template) => template.variantCount > 0).length}
            tone="text-gold"
          />
        </div>
      )}

      <DataTable
        columns={columns}
        data={result?.items ?? []}
        searchPlaceholder={t('admin.templates.mobilesSearch')}
        search={{ value: search, onChange: changeSearch }}
        pagination={{ page, totalPages: result?.totalPages ?? 1, onPageChange: setPage }}
      />

      {result && result.items.length === 0 && <p className="text-sm text-muted">{t('admin.templates.empty')}</p>}
    </div>
  )
}
