import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { DataTable } from '../components/ui/data-table'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { useAccounts, type Account } from '../lib/accounts'
import { isAdmin } from '../lib/roles'
import { NewAccountDialog } from './accounts/NewAccountDialog'
import { EditAccountDialog } from './accounts/EditAccountDialog'

export function AccountsScreen() {
  const { t } = useTranslation()
  const accounts = useAccounts()
  const [editing, setEditing] = useState<Account | null>(null)
  const [creating, setCreating] = useState(false)

  const columns = useMemo<ColumnDef<Account>[]>(
    () => [
      { accessorKey: 'username', header: t('admin.accounts.username') },
      {
        accessorKey: 'email',
        header: t('admin.accounts.email'),
        cell: ({ getValue }) => (getValue() as string | null) ?? t('admin.accounts.none'),
      },
      {
        accessorKey: 'level',
        header: t('admin.accounts.level'),
        cell: ({ getValue }) => {
          const level = getValue() as string
          return <Badge variant={isAdmin(level) ? 'staff' : 'info'}>{level}</Badge>
        },
      },
      {
        accessorKey: 'isActive',
        header: t('admin.accounts.status'),
        cell: ({ getValue }) =>
          (getValue() as boolean) ? (
            <Badge variant="success">{t('admin.accounts.active')}</Badge>
          ) : (
            <Badge variant="danger">{t('admin.accounts.suspended')}</Badge>
          ),
      },
      { accessorKey: 'characterCount', header: t('admin.accounts.characters') },
      {
        id: 'actions',
        header: '',
        cell: ({ row }) => (
          <Button variant="default" className="px-3 py-1 text-xs" onClick={() => setEditing(row.original)}>
            {t('admin.accounts.manage')}
          </Button>
        ),
      },
    ],
    [t],
  )

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="font-display text-xl text-ink">{t('admin.accounts.title')}</h1>
        <Button onClick={() => setCreating(true)}>{t('admin.accounts.new')}</Button>
      </div>

      <DataTable columns={columns} data={accounts.data ?? []} searchPlaceholder={t('admin.accounts.search')} />

      <NewAccountDialog open={creating} onOpenChange={setCreating} />
      <EditAccountDialog account={editing} onOpenChange={(open) => !open && setEditing(null)} />
    </div>
  )
}
