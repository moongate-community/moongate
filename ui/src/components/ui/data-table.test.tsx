import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ColumnDef } from '@tanstack/react-table'
import { DataTable } from './data-table'

type Row = { name: string; level: string }

const columns: ColumnDef<Row>[] = [
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'level', header: 'Level' },
]

const rows: Row[] = [
  { name: 'aelric', level: 'Player' },
  { name: 'althea', level: 'GrandMaster' },
]

describe('DataTable', () => {
  it('renders rows and filters them by the search box', async () => {
    render(<DataTable columns={columns} data={rows} searchPlaceholder="Search" />)

    expect(screen.getByText('aelric')).toBeInTheDocument()
    expect(screen.getByText('althea')).toBeInTheDocument()

    await userEvent.type(screen.getByPlaceholderText('Search'), 'alth')

    expect(screen.queryByText('aelric')).not.toBeInTheDocument()
    expect(screen.getByText('althea')).toBeInTheDocument()
  })

  it('sorts when a header is clicked', async () => {
    render(<DataTable columns={columns} data={rows} />)

    await userEvent.click(screen.getByRole('button', { name: /name/i }))
    const cells = screen.getAllByRole('cell').map((c) => c.textContent)
    // ascending by name puts aelric before althea
    expect(cells.indexOf('aelric')).toBeLessThan(cells.indexOf('althea'))
  })
})
