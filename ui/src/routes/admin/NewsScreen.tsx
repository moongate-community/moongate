import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'

import { DataTable } from '../../components/ui/data-table'
import { StatCard } from '../../components/ui/stat-card'
import { ScreenTitle } from '../../components/ui/section-title'
import { Badge } from '../../components/ui/badge'
import { Button } from '../../components/ui/button'
import { toast } from '../../components/ui/sonner'
import { NewsDialog } from '../news/NewsDialog'
import { useDeleteNews, useNews, type News } from '../../lib/news'

export function NewsScreen() {
  const { t } = useTranslation()
  const news = useNews()
  const remove = useDeleteNews()
  const [editing, setEditing] = useState<News | null>(null)
  const [open, setOpen] = useState(false)

  async function confirmDelete(entry: News) {
    if (!globalThis.confirm(t('admin.news.deleteConfirm', { title: entry.title }))) {
      return
    }

    try {
      await remove.mutateAsync(entry.id)
    } catch {
      toast.error(t('error.generic'))
    }
  }

  const columns = useMemo<ColumnDef<News>[]>(
    () => [
      { accessorKey: 'title', header: t('admin.news.newsTitle') },
      { accessorKey: 'author', header: t('admin.news.author') },
      {
        accessorKey: 'isPublished',
        header: t('admin.news.status'),
        cell: ({ getValue }) =>
          (getValue() as boolean) ? (
            <Badge variant="success">{t('admin.news.published')}</Badge>
          ) : (
            <Badge variant="info">{t('admin.news.draft')}</Badge>
          ),
      },
      {
        accessorKey: 'updatedAt',
        header: t('admin.news.updated'),
        cell: ({ getValue }) => (
          <span className="font-mono text-xs text-muted">{new Date(getValue() as string).toLocaleString()}</span>
        ),
      },
      {
        id: 'actions',
        header: '',
        cell: ({ row }) => (
          <span className="flex gap-2">
            <Button
              variant="default"
              className="px-3 py-1 text-xs"
              onClick={() => {
                setEditing(row.original)
                setOpen(true)
              }}
            >
              {t('admin.news.edit')}
            </Button>
            <Button variant="outline" className="px-3 py-1 text-xs" onClick={() => confirmDelete(row.original)}>
              {t('admin.news.delete')}
            </Button>
          </span>
        ),
      },
    ],
    // confirmDelete closes over `remove` and `t`, both stable enough for a row action.
    [t],
  )

  if (news.isError) {
    return (
      <p role="alert" className="text-sm text-danger-text">
        {t('admin.news.loadFailed')}
      </p>
    )
  }

  return (
    <div className="flex flex-col gap-4">
      <ScreenTitle
        action={
          <Button
            onClick={() => {
              setEditing(null)
              setOpen(true)
            }}
          >
            {t('admin.news.new')}
          </Button>
        }
      >
        {t('admin.news.title')}
      </ScreenTitle>

      {news.data && (
        <div className="grid gap-4 sm:grid-cols-3">
          <StatCard label={t('admin.news.total')} value={news.data.length} />
          <StatCard
            label={t('admin.news.publishedCount')}
            value={news.data.filter((entry) => entry.isPublished).length}
            tone="text-gold"
          />
          <StatCard label={t('admin.news.draftCount')} value={news.data.filter((entry) => !entry.isPublished).length} />
        </div>
      )}

      <DataTable columns={columns} data={news.data ?? []} searchPlaceholder={t('admin.news.search')} />

      {news.data?.length === 0 && <p className="text-sm text-muted">{t('admin.news.empty')}</p>}

      <NewsDialog entry={editing} open={open} onOpenChange={setOpen} />
    </div>
  )
}
