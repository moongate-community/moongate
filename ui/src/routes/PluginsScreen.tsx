import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { DataTable } from '../components/ui/data-table'
import { Badge } from '../components/ui/badge'
import { useAdminPlugins, type PluginInfo } from '../lib/queries'

// The activated plugins in the kit's searchable, sortable DataTable: name, version, declared route count,
// and an external/host badge. Moved out of the overview into its own admin page.
export function PluginsScreen() {
  const { t } = useTranslation()
  const plugins = useAdminPlugins()

  const columns = useMemo<ColumnDef<PluginInfo>[]>(
    () => [
      { accessorKey: 'name', header: t('admin.plugins.name') },
      {
        accessorKey: 'version',
        header: t('admin.plugins.version'),
        cell: ({ getValue }) => <span className="font-mono text-xs">{getValue() as string}</span>,
      },
      {
        id: 'routes',
        accessorFn: (plugin) => plugin.routes.length,
        header: t('admin.plugins.routesHeader'),
      },
      {
        accessorKey: 'isExternal',
        header: t('admin.plugins.type'),
        cell: ({ getValue }) =>
          (getValue() as boolean) ? (
            <Badge variant="info">{t('admin.plugins.external')}</Badge>
          ) : (
            <Badge variant="staff">{t('admin.plugins.host')}</Badge>
          ),
      },
    ],
    [t],
  )

  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-xl text-ink">{t('admin.plugins.title')}</h1>

      {plugins.isError && (
        <p role="alert" className="text-sm text-danger-text">
          {t('error.generic')}
        </p>
      )}

      <DataTable columns={columns} data={plugins.data ?? []} searchPlaceholder={t('admin.plugins.search')} />
    </div>
  )
}
