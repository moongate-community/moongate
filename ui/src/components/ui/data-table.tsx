import { useState } from 'react'
import {
  type ColumnDef,
  type SortingState,
  flexRender,
  getCoreRowModel,
  getFilteredRowModel,
  getSortedRowModel,
  useReactTable,
} from '@tanstack/react-table'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from './table'
import { Input } from './input'

// A searchable, sortable table over the styled primitives. Search is a global filter; each header is a
// button that toggles that column's sort. Behaviour comes from TanStack Table; the look from the tokens.
export function DataTable<T>({
  columns,
  data,
  searchPlaceholder,
}: {
  columns: ColumnDef<T>[]
  data: T[]
  searchPlaceholder?: string
}) {
  const [globalFilter, setGlobalFilter] = useState('')
  const [sorting, setSorting] = useState<SortingState>([])

  const table = useReactTable({
    data,
    columns,
    state: { globalFilter, sorting },
    onGlobalFilterChange: setGlobalFilter,
    onSortingChange: setSorting,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getSortedRowModel: getSortedRowModel(),
  })

  return (
    <div className="flex flex-col gap-3">
      {searchPlaceholder !== undefined && (
        <Input
          value={globalFilter}
          placeholder={searchPlaceholder}
          onChange={(e) => setGlobalFilter(e.target.value)}
          className="max-w-xs"
        />
      )}
      <Table>
        <TableHeader>
          {table.getHeaderGroups().map((group) => (
            <TableRow key={group.id}>
              {group.headers.map((header) => (
                <TableHead key={header.id}>
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
                </TableHead>
              ))}
            </TableRow>
          ))}
        </TableHeader>
        <TableBody>
          {table.getRowModel().rows.map((row) => (
            <TableRow key={row.id}>
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
    </div>
  )
}
