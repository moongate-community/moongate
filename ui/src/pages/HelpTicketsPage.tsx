import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Button,
  Chip,
  Select,
  SelectItem,
  Spinner,
  Table,
  TableBody,
  TableCell,
  TableColumn,
  TableHeader,
  TableRow,
} from '@heroui/react'
import { api } from '../api/client'

interface HelpTicketItem {
  ticketId: string
  senderCharacterId: string
  senderAccountId: string
  category: string
  message: string
  status: string
  mapId: number
  x: number
  y: number
  z: number
  createdAtUtc: string
  assignedAtUtc: string | null
  closedAtUtc: string | null
  lastUpdatedAtUtc: string
  assignedToCharacterId: string
  assignedToAccountId: string
}

interface HelpTicketPage {
  page: number
  pageSize: number
  totalCount: number
  items: HelpTicketItem[]
}

const PAGE_SIZES = [20, 50, 100]
const STATUS_OPTIONS = ['all', 'Open', 'Assigned', 'Closed'] as const
const CATEGORY_OPTIONS = [
  'all',
  'Question',
  'Stuck',
  'Bug',
  'Account',
  'Suggestion',
  'Other',
  'VerbalHarassment',
  'PhysicalHarassment',
] as const

function toFriendlyLabel(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2')
}

function statusColor(status: string): 'default' | 'primary' | 'secondary' | 'success' | 'warning' | 'danger' {
  switch (status) {
    case 'Open':
      return 'primary'
    case 'Assigned':
      return 'warning'
    case 'Closed':
      return 'success'
    default:
      return 'default'
  }
}

export function HelpTicketsPage() {
  const navigate = useNavigate()
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [statusFilter, setStatusFilter] = useState<string>('all')
  const [categoryFilter, setCategoryFilter] = useState<string>('all')
  const [assignedToMe, setAssignedToMe] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [result, setResult] = useState<HelpTicketPage>({
    page: 1,
    pageSize: 20,
    totalCount: 0,
    items: [],
  })

  useEffect(() => {
    let mounted = true

    async function load() {
      setLoading(true)
      setError(null)

      try {
        const query = new URLSearchParams()
        query.set('page', String(page))
        query.set('pageSize', String(pageSize))
        if (statusFilter !== 'all') {
          query.set('status', statusFilter)
        }
        if (categoryFilter !== 'all') {
          query.set('category', categoryFilter)
        }
        if (assignedToMe) {
          query.set('assignedToMe', 'true')
        }

        const payload = await api.get<HelpTicketPage>(`/help-tickets?${query.toString()}`)
        if (mounted) {
          setResult(payload)
        }
      } catch (fetchError) {
        if (mounted) {
          setError(fetchError instanceof Error ? fetchError.message : 'Failed to load help tickets.')
        }
      } finally {
        if (mounted) {
          setLoading(false)
        }
      }
    }

    load()

    return () => {
      mounted = false
    }
  }, [page, pageSize, statusFilter, categoryFilter, assignedToMe])

  const totalPages = useMemo(
    () => Math.max(1, Math.ceil(result.totalCount / Math.max(result.pageSize, 1))),
    [result.pageSize, result.totalCount],
  )

  function goPreviousPage() {
    setPage((currentPage) => Math.max(1, currentPage - 1))
  }

  function goNextPage() {
    setPage((currentPage) => Math.min(totalPages, currentPage + 1))
  }

  function resetFilters() {
    setPage(1)
    setStatusFilter('all')
    setCategoryFilter('all')
    setAssignedToMe(false)
  }

  return (
    <div className="flex flex-col gap-6 animate-fade-in w-full max-w-[1360px] mx-auto">
      <div className="flex items-end justify-between gap-4 flex-wrap">
        <div>
          <div className="flex items-center gap-3 mb-1">
            <div
              style={{
                width: '2px',
                height: '20px',
                background: '#6aa5da',
                borderRadius: '1px',
                boxShadow: '0 0 6px rgba(106,165,218,0.5)',
              }}
            />
            <h1
              className="font-cinzel font-semibold tracking-wider"
              style={{ color: '#f9f4ed', fontSize: '18px', letterSpacing: '0.12em' }}
            >
              Help Tickets
            </h1>
          </div>
          <p className="font-mono text-xs pl-5" style={{ color: 'rgba(185,187,211,0.35)', letterSpacing: '0.1em' }}>
            STAFF QUEUE AND INCIDENT REVIEW
          </p>
        </div>

        <div className="flex items-center gap-3 flex-wrap">
          <Chip
            variant="flat"
            className="font-mono text-xs tracking-wider uppercase"
            style={{
              background: 'rgba(106,165,218,0.1)',
              border: '1px solid rgba(106,165,218,0.2)',
              color: '#6aa5da',
            }}
          >
            Total: {result.totalCount}
          </Chip>

          <Select
            disallowEmptySelection
            size="sm"
            label="Rows"
            labelPlacement="outside-left"
            selectedKeys={[String(pageSize)]}
            onSelectionChange={(keys) => {
              const firstKey = Array.from(keys)[0]
              const parsedPageSize = Number(firstKey)
              if (!Number.isNaN(parsedPageSize)) {
                setPage(1)
                setPageSize(parsedPageSize)
              }
            }}
            className="w-44"
            classNames={{
              trigger: 'bg-[rgba(36,33,48,0.7)] border border-[rgba(106,165,218,0.2)]',
              value: 'font-mono text-xs text-[#f9f4ed]',
              label: 'font-mono text-xs text-[rgba(185,187,211,0.6)]',
              selectorIcon: 'text-[#6aa5da]',
              popoverContent: 'bg-[#242130] border border-[rgba(106,165,218,0.2)]',
            }}
          >
            {PAGE_SIZES.map((size) => (
              <SelectItem key={String(size)} className="font-mono text-xs">
                {size}
              </SelectItem>
            ))}
          </Select>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-[1fr_1fr_auto_auto] gap-3 rounded-xl border border-[rgba(106,165,218,0.15)] bg-[rgba(36,33,48,0.5)] backdrop-blur-md p-3">
        <Select
          disallowEmptySelection
          label="Status"
          selectedKeys={[statusFilter]}
          onSelectionChange={(keys) => {
            const firstKey = Array.from(keys)[0]
            if (typeof firstKey === 'string') {
              setPage(1)
              setStatusFilter(firstKey)
            }
          }}
          classNames={{
            trigger: 'bg-[rgba(31,28,42,0.85)] border-[rgba(106,165,218,0.2)]',
            value: 'font-mono text-sm text-[#f9f4ed]',
            label: 'text-[rgba(185,187,211,0.6)] font-mono text-xs tracking-wider',
            selectorIcon: 'text-[#6aa5da]',
            popoverContent: 'bg-[#242130] border border-[rgba(106,165,218,0.2)]',
          }}
        >
          {STATUS_OPTIONS.map((option) => (
            <SelectItem key={option} className="font-mono text-xs">
              {option === 'all' ? 'All statuses' : option}
            </SelectItem>
          ))}
        </Select>

        <Select
          disallowEmptySelection
          label="Category"
          selectedKeys={[categoryFilter]}
          onSelectionChange={(keys) => {
            const firstKey = Array.from(keys)[0]
            if (typeof firstKey === 'string') {
              setPage(1)
              setCategoryFilter(firstKey)
            }
          }}
          classNames={{
            trigger: 'bg-[rgba(31,28,42,0.85)] border-[rgba(106,165,218,0.2)]',
            value: 'font-mono text-sm text-[#f9f4ed]',
            label: 'text-[rgba(185,187,211,0.6)] font-mono text-xs tracking-wider',
            selectorIcon: 'text-[#6aa5da]',
            popoverContent: 'bg-[#242130] border border-[rgba(106,165,218,0.2)]',
          }}
        >
          {CATEGORY_OPTIONS.map((option) => (
            <SelectItem key={option} className="font-mono text-xs">
              {option === 'all' ? 'All categories' : toFriendlyLabel(option)}
            </SelectItem>
          ))}
        </Select>

        <Button
          variant={assignedToMe ? 'flat' : 'bordered'}
          color="primary"
          className="font-mono text-xs uppercase tracking-wider self-end"
          onPress={() => {
            setPage(1)
            setAssignedToMe((value) => !value)
          }}
        >
          {assignedToMe ? 'Assigned To Me' : 'Any Assignee'}
        </Button>

        <Button
          variant="bordered"
          className="font-mono text-xs uppercase tracking-wider border-[rgba(106,165,218,0.25)] text-[rgba(185,187,211,0.8)] self-end"
          onPress={resetFilters}
        >
          Reset
        </Button>
      </div>

      {error && (
        <div className="rounded-lg border border-[rgba(239,68,68,0.28)] bg-[rgba(239,68,68,0.1)] px-4 py-3">
          <p className="font-mono text-xs uppercase tracking-wider text-[#ef4444]">Error: {error}</p>
        </div>
      )}

      <div className="rounded-xl border border-[rgba(106,165,218,0.15)] bg-[rgba(36,33,48,0.7)] backdrop-blur-md p-2">
        <Table
          aria-label="Help tickets table"
          classNames={{
            wrapper: 'bg-transparent shadow-none',
            th: 'bg-[rgba(106,165,218,0.1)] text-[#6aa5da] font-mono text-xs tracking-widest uppercase border-b border-[rgba(106,165,218,0.15)]',
            td: 'border-b border-[rgba(106,165,218,0.05)] py-3 font-mono text-sm',
          }}
          onRowAction={(key) => navigate(`/help-tickets/${String(key)}`)}
        >
          <TableHeader>
            <TableColumn>TICKET</TableColumn>
            <TableColumn>CATEGORY</TableColumn>
            <TableColumn>STATUS</TableColumn>
            <TableColumn>SENDER</TableColumn>
            <TableColumn>LOCATION</TableColumn>
            <TableColumn>CREATED</TableColumn>
            <TableColumn>ASSIGNED</TableColumn>
          </TableHeader>
          <TableBody
            items={result.items}
            isLoading={loading}
            loadingContent={<Spinner color="primary" />}
            emptyContent={error ? 'Unable to load help tickets.' : 'No help tickets found.'}
          >
            {(ticket) => (
              <TableRow key={ticket.ticketId}>
                <TableCell>
                  <span className="text-[#f9f4ed]">#{ticket.ticketId}</span>
                </TableCell>
                <TableCell>
                  <span className="text-[rgba(185,187,211,0.9)]">{toFriendlyLabel(ticket.category)}</span>
                </TableCell>
                <TableCell>
                  <Chip size="sm" variant="flat" color={statusColor(ticket.status)} className="font-mono text-xs uppercase">
                    {ticket.status}
                  </Chip>
                </TableCell>
                <TableCell>
                  <div className="flex flex-col gap-0.5">
                    <span className="text-[#6aa5da]">Account #{ticket.senderAccountId}</span>
                    <span className="text-[rgba(185,187,211,0.6)] text-xs">Character #{ticket.senderCharacterId}</span>
                  </div>
                </TableCell>
                <TableCell>
                  <span className="text-[rgba(185,187,211,0.8)]">
                    M{ticket.mapId} · {ticket.x},{ticket.y},{ticket.z}
                  </span>
                </TableCell>
                <TableCell>
                  <span className="text-xs text-[rgba(185,187,211,0.6)]">
                    {new Date(ticket.createdAtUtc).toLocaleString()}
                  </span>
                </TableCell>
                <TableCell>
                  <span className="text-[rgba(185,187,211,0.8)]">
                    {ticket.assignedToAccountId ? `#${ticket.assignedToAccountId}` : '-'}
                  </span>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>

      <div className="flex items-center justify-between gap-3 flex-wrap">
        <p className="font-mono text-xs uppercase tracking-wider text-[rgba(185,187,211,0.55)]">
          Page {result.page} of {totalPages}
        </p>

        <div className="flex items-center gap-2">
          <Button
            variant="bordered"
            className="font-mono text-xs uppercase tracking-wider border-[rgba(106,165,218,0.25)]"
            onPress={goPreviousPage}
            isDisabled={page <= 1}
          >
            Previous
          </Button>
          <Button
            color="primary"
            variant="flat"
            className="font-mono text-xs uppercase tracking-wider"
            onPress={goNextPage}
            isDisabled={page >= totalPages}
          >
            Next
          </Button>
        </div>
      </div>
    </div>
  )
}
