import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { DataTable } from '../components/ui/data-table'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { ScreenTitle } from '../components/ui/section-title'
import { useAccounts, type Account } from '../lib/accounts'
import { isAdmin } from '../lib/roles'
import { NewAccountDialog } from './accounts/NewAccountDialog'
import { AccountDetailPanel } from './accounts/AccountDetailPanel'

export function AccountsScreen() {
  const { t } = useTranslation()
  const accounts = useAccounts()
  const [selected, setSelected] = useState<Account | null>(null)
  const [creating, setCreating] = useState(false)

  // The row carries the account the list was drawn from; the panel needs the freshest copy, so it is
  // looked up again by name on every render. Suspending an account otherwise leaves the panel
  // showing the state it had when it was picked.
  const shown = selected && (accounts.data?.find((a) => a.username === selected.username) ?? null)

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
      {
        accessorKey: 'characterCount',
        header: t('admin.accounts.characters'),
        cell: ({ getValue }) => <span className="font-mono text-xs">{getValue() as number}</span>,
      },
    ],
    [t],
  )

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <ScreenTitle>{t('admin.accounts.title')}</ScreenTitle>
        <Button onClick={() => setCreating(true)}>{t('admin.accounts.new')}</Button>
      </div>

      <div className="grid items-start gap-4 lg:grid-cols-[minmax(0,1fr)_22rem]">
        <DataTable
          columns={columns}
          data={accounts.data ?? []}
          searchPlaceholder={t('admin.accounts.search')}
          onRowClick={setSelected}
          isRowSelected={(row) => row.username === selected?.username}
        />

        <AccountDetailPanel account={shown ?? null} />
      </div>

      <NewAccountDialog open={creating} onOpenChange={setCreating} />
    </div>
  )
}
