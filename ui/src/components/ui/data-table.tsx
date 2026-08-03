import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  type ColumnDef,
  type SortingState,
  flexRender,
  getCoreRowModel,
  getFilteredRowModel,
  getSortedRowModel,
  useReactTable,
} from '@tanstack/react-table'
import { cn } from '@/lib/utils'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from './table'
import { Input } from './input'
import { Button } from './button'

/** A search box the caller owns, because the server does the filtering. */
export type DataTableSearch = { value: string; onChange: (value: string) => void }

/** A pager the caller owns, because the server does the paging. */
export type DataTablePagination = { page: number; totalPages: number; onPageChange: (page: number) => void }

// A searchable, sortable table over the styled primitives. Search is a global filter; each header is a
// button that toggles that column's sort. Behaviour comes from TanStack Table; the look from the tokens.
//
// Pass `search` and/or `pagination` when the SERVER does that work instead. Each switches off the
// matching client-side behaviour, and that is the point rather than a detail: filtering or sorting a
// single page of a paged result operates on that page alone, so a search would report "not found"
// for a row sitting on page 2.
export function DataTable<T>({
  columns,
  data,
  searchPlaceholder,
  search,
  pagination,
  onRowClick,
  isRowSelected,
}: {
  columns: ColumnDef<T>[]
  data: T[]
  searchPlaceholder?: string
  search?: DataTableSearch
  pagination?: DataTablePagination
  /** Makes rows pick something. Given, the table becomes the master half of a master-detail screen. */
  onRowClick?: (row: T) => void
  /** Which row is currently picked, so it can be marked while its detail is open. */
  isRowSelected?: (row: T) => boolean
}) {
  const { t } = useTranslation()
  const [globalFilter, setGlobalFilter] = useState('')
  const [sorting, setSorting] = useState<SortingState>([])

  const serverSearched = search !== undefined
  const serverPaged = pagination !== undefined

  const table = useReactTable({
    data,
    columns,
    state: { globalFilter: serverSearched ? '' : globalFilter, sorting: serverPaged ? [] : sorting },
    onGlobalFilterChange: setGlobalFilter,
    onSortingChange: setSorting,
    getCoreRowModel: getCoreRowModel(),
    ...(serverSearched ? {} : { getFilteredRowModel: getFilteredRowModel() }),
    ...(serverPaged ? {} : { getSortedRowModel: getSortedRowModel() }),
  })

  return (
    <div className="flex flex-col gap-3">
      {searchPlaceholder !== undefined && (
        <Input
          value={search ? search.value : globalFilter}
          placeholder={searchPlaceholder}
          onChange={(e) => (search ? search.onChange(e.target.value) : setGlobalFilter(e.target.value))}
          className="max-w-xs"
        />
      )}
      <Table>
        <TableHeader>
          {table.getHeaderGroups().map((group) => (
            <TableRow key={group.id}>
              {group.headers.map((header) => (
                <TableHead key={header.id}>
                  {serverPaged ? (
                    <span className="flex items-center gap-1 uppercase">
                      {flexRender(header.column.columnDef.header, header.getContext())}
                    </span>
                  ) : (
                    <button
                      type="button"
                      className="flex items-center gap-1 uppercase"
                      onClick={header.column.getToggleSortingHandler()}
                    >
                      {flexRender(header.column.columnDef.header, header.getContext())}
                      <span className="text-gold">
                        {{ asc: '▲', desc: '▼' }[header.column.getIsSorted() as string] ?? ''}
                      </span>
                    </button>
                  )}
                </TableHead>
              ))}
            </TableRow>
          ))}
        </TableHeader>
        <TableBody>
          {table.getRowModel().rows.map((row) => (
            <TableRow
              key={row.id}
              onClick={onRowClick ? () => onRowClick(row.original) : undefined}
              data-selected={isRowSelected?.(row.original) ? 'true' : undefined}
              className={cn(
                onRowClick && 'cursor-pointer',
                isRowSelected?.(row.original) &&
                  'bg-gold/10 [&>td:first-child]:border-l-2 [&>td:first-child]:border-gold',
              )}
            >
              {row.getVisibleCells().map((cell) => (
                <TableCell key={cell.id}>
                  {cell.column.columnDef.cell
                    ? flexRender(cell.column.columnDef.cell, cell.getContext())
                    : String(cell.getValue() ?? '')}
                </TableCell>
              ))}
            </TableRow>
          ))}
        </TableBody>
      </Table>

      {pagination && (
        <div className="flex items-center justify-between">
          <span className="text-sm text-muted">
            {t('common.pageOf', { page: pagination.page, total: Math.max(pagination.totalPages, 1) })}
          </span>
          <div className="flex gap-2">
            <Button
              variant="default"
              className="px-3 py-1 text-xs"
              disabled={pagination.page <= 1}
              onClick={() => pagination.onPageChange(pagination.page - 1)}
            >
              {t('common.previous')}
            </Button>
            <Button
              variant="default"
              className="px-3 py-1 text-xs"
              disabled={pagination.page >= pagination.totalPages}
              onClick={() => pagination.onPageChange(pagination.page + 1)}
            >
              {t('common.next')}
            </Button>
          </div>
        </div>
      )}
    </div>
  )
}
