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

  it('renders no pager when pagination is not given', () => {
    render(<DataTable columns={columns} data={rows} />)

    expect(screen.queryByRole('button', { name: /next/i })).not.toBeInTheDocument()
  })

  // The trap the controlled props exist for: with a server-driven search, filtering here would
  // search only the page in hand and silently hide every match on another page.
  it('does not filter in the browser when the search is controlled', async () => {
    const onChange = vi.fn()

    render(<DataTable columns={columns} data={rows} searchPlaceholder="Search" search={{ value: '', onChange }} />)

    await userEvent.type(screen.getByPlaceholderText('Search'), 'alth')

    expect(onChange).toHaveBeenCalled()
    expect(screen.getByText('aelric')).toBeInTheDocument()
    expect(screen.getByText('althea')).toBeInTheDocument()
  })

  it('shows the controlled search value', () => {
    render(
      <DataTable
        columns={columns}
        data={rows}
        searchPlaceholder="Search"
        search={{ value: 'typed', onChange: vi.fn() }}
      />,
    )

    expect(screen.getByPlaceholderText('Search')).toHaveValue('typed')
  })

  it('reports which page was asked for', async () => {
    const onPageChange = vi.fn()

    render(<DataTable columns={columns} data={rows} pagination={{ page: 2, totalPages: 5, onPageChange }} />)

    await userEvent.click(screen.getByRole('button', { name: /next/i }))
    expect(onPageChange).toHaveBeenCalledWith(3)

    await userEvent.click(screen.getByRole('button', { name: /previous/i }))
    expect(onPageChange).toHaveBeenCalledWith(1)
  })

  it('cannot page past either end', () => {
    const { rerender } = render(
      <DataTable columns={columns} data={rows} pagination={{ page: 1, totalPages: 3, onPageChange: vi.fn() }} />,
    )

    expect(screen.getByRole('button', { name: /previous/i })).toBeDisabled()

    rerender(<DataTable columns={columns} data={rows} pagination={{ page: 3, totalPages: 3, onPageChange: vi.fn() }} />)

    expect(screen.getByRole('button', { name: /next/i })).toBeDisabled()
  })

  // Sorting one page of a server-paged list reorders that page alone, which reads as "the strongest
  // character" while meaning "the strongest on page 3".
  it('offers no sort buttons while server-paged', () => {
    render(<DataTable columns={columns} data={rows} pagination={{ page: 1, totalPages: 2, onPageChange: vi.fn() }} />)

    expect(screen.queryByRole('button', { name: /name/i })).not.toBeInTheDocument()
    expect(screen.getByText('Name')).toBeInTheDocument()
  })
})
