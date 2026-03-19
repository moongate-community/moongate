import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { Button, Chip, Spinner } from '@heroui/react'
import { api } from '../api/client'

interface HelpTicketDetail {
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

export function HelpTicketDetailsPage() {
  const { ticketId } = useParams()
  const navigate = useNavigate()
  const [loading, setLoading] = useState(true)
  const [actionLoading, setActionLoading] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [ticket, setTicket] = useState<HelpTicketDetail | null>(null)

  useEffect(() => {
    if (!ticketId) {
      setError('Missing ticket id.')
      setLoading(false)
      return
    }

    const currentTicketId = ticketId
    let mounted = true

    async function load() {
      setLoading(true)
      setError(null)

      try {
        const payload = await api.get<HelpTicketDetail>(`/help-tickets/${encodeURIComponent(currentTicketId)}`)
        if (mounted) {
          setTicket(payload)
        }
      } catch (fetchError) {
        if (mounted) {
          setError(fetchError instanceof Error ? fetchError.message : 'Failed to load help ticket.')
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
  }, [ticketId])

  async function takeOwnership() {
    if (!ticketId) {
      return
    }

    setActionLoading('assign')
    setError(null)
    try {
      const payload = await api.put<HelpTicketDetail>(`/help-tickets/${encodeURIComponent(ticketId)}/assign-to-me`, {})
      setTicket(payload)
    } catch (actionError) {
      setError(actionError instanceof Error ? actionError.message : 'Failed to assign ticket.')
    } finally {
      setActionLoading(null)
    }
  }

  async function changeStatus(nextStatus: 'Open' | 'Assigned' | 'Closed') {
    if (!ticketId) {
      return
    }

    setActionLoading(nextStatus)
    setError(null)
    try {
      const payload = await api.put<HelpTicketDetail>(
        `/help-tickets/${encodeURIComponent(ticketId)}/status`,
        { status: nextStatus },
      )
      setTicket(payload)
    } catch (actionError) {
      setError(actionError instanceof Error ? actionError.message : 'Failed to update ticket status.')
    } finally {
      setActionLoading(null)
    }
  }

  return (
    <div className="flex flex-col gap-6 animate-fade-in w-full max-w-[1200px] mx-auto">
      <div className="flex items-center justify-between gap-4 flex-wrap">
        <div className="flex items-center gap-3">
          <Button
            size="sm"
            variant="flat"
            color="primary"
            className="font-mono text-xs uppercase tracking-wider"
            onPress={() => navigate('/help-tickets')}
          >
            Back
          </Button>
          <div>
            <h1 className="font-cinzel font-semibold tracking-wider text-[#f9f4ed] text-lg">Help Ticket Details</h1>
            <p className="font-mono text-xs text-[rgba(185,187,211,0.45)] tracking-wider uppercase">
              {ticketId ? `Ticket #${ticketId}` : 'Unknown ticket'}
            </p>
          </div>
        </div>
      </div>

      {loading && (
        <div className="rounded-xl border border-[rgba(106,165,218,0.15)] bg-[rgba(36,33,48,0.6)] p-8 flex items-center gap-3">
          <Spinner size="sm" color="primary" />
          <span className="font-mono text-xs uppercase tracking-wider text-[rgba(185,187,211,0.8)]">Loading details...</span>
        </div>
      )}

      {!loading && error && (
        <div className="rounded-lg border border-[rgba(239,68,68,0.28)] bg-[rgba(239,68,68,0.1)] px-4 py-3">
          <p className="font-mono text-xs uppercase tracking-wider text-[#ef4444]">{error}</p>
        </div>
      )}

      {!loading && !error && ticket && (
        <div className="flex flex-col gap-4">
          <div className="flex items-center gap-3 flex-wrap">
            <Chip size="sm" variant="flat" color={statusColor(ticket.status)} className="font-mono text-xs uppercase">
              {ticket.status}
            </Chip>
            <Chip
              variant="flat"
              className="font-mono text-xs tracking-wider uppercase"
              style={{
                background: 'rgba(106,165,218,0.1)',
                border: '1px solid rgba(106,165,218,0.2)',
                color: '#6aa5da',
              }}
            >
              {toFriendlyLabel(ticket.category)}
            </Chip>
            <span className="font-mono text-xs uppercase tracking-wider text-[rgba(185,187,211,0.6)]">
              Created {new Date(ticket.createdAtUtc).toLocaleString()}
            </span>
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
            <div className="rounded-xl border border-[rgba(106,165,218,0.15)] bg-[rgba(36,33,48,0.7)] backdrop-blur-md p-4 space-y-3">
              <h2 className="font-mono text-xs uppercase tracking-wider text-[#6aa5da]">Sender</h2>
              <div className="grid grid-cols-2 gap-3">
                <div className="rounded-lg border border-[rgba(106,165,218,0.12)] bg-[rgba(31,28,42,0.7)] px-3 py-2">
                  <p className="font-mono text-[10px] uppercase tracking-wider text-[rgba(185,187,211,0.6)]">Account</p>
                  <p className="font-mono text-sm text-[#f9f4ed]">#{ticket.senderAccountId}</p>
                </div>
                <div className="rounded-lg border border-[rgba(106,165,218,0.12)] bg-[rgba(31,28,42,0.7)] px-3 py-2">
                  <p className="font-mono text-[10px] uppercase tracking-wider text-[rgba(185,187,211,0.6)]">Character</p>
                  <p className="font-mono text-sm text-[#f9f4ed]">#{ticket.senderCharacterId}</p>
                </div>
              </div>

              <div className="rounded-lg border border-[rgba(106,165,218,0.12)] bg-[rgba(31,28,42,0.7)] px-3 py-2">
                <p className="font-mono text-[10px] uppercase tracking-wider text-[rgba(185,187,211,0.6)]">Location</p>
                <p className="font-mono text-sm text-[#f9f4ed]">
                  Map {ticket.mapId} · {ticket.x}, {ticket.y}, {ticket.z}
                </p>
              </div>
            </div>

            <div className="rounded-xl border border-[rgba(106,165,218,0.15)] bg-[rgba(36,33,48,0.7)] backdrop-blur-md p-4 space-y-3">
              <h2 className="font-mono text-xs uppercase tracking-wider text-[#6aa5da]">Assignment</h2>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div className="rounded-lg border border-[rgba(106,165,218,0.12)] bg-[rgba(31,28,42,0.7)] px-3 py-2">
                  <p className="font-mono text-[10px] uppercase tracking-wider text-[rgba(185,187,211,0.6)]">Assigned Account</p>
                  <p className="font-mono text-sm text-[#f9f4ed]">
                    {ticket.assignedToAccountId ? `#${ticket.assignedToAccountId}` : '-'}
                  </p>
                </div>
                <div className="rounded-lg border border-[rgba(106,165,218,0.12)] bg-[rgba(31,28,42,0.7)] px-3 py-2">
                  <p className="font-mono text-[10px] uppercase tracking-wider text-[rgba(185,187,211,0.6)]">Assigned Character</p>
                  <p className="font-mono text-sm text-[#f9f4ed]">
                    {ticket.assignedToCharacterId ? `#${ticket.assignedToCharacterId}` : '-'}
                  </p>
                </div>
                <div className="rounded-lg border border-[rgba(106,165,218,0.12)] bg-[rgba(31,28,42,0.7)] px-3 py-2">
                  <p className="font-mono text-[10px] uppercase tracking-wider text-[rgba(185,187,211,0.6)]">Assigned At</p>
                  <p className="font-mono text-sm text-[#f9f4ed]">
                    {ticket.assignedAtUtc ? new Date(ticket.assignedAtUtc).toLocaleString() : '-'}
                  </p>
                </div>
                <div className="rounded-lg border border-[rgba(106,165,218,0.12)] bg-[rgba(31,28,42,0.7)] px-3 py-2">
                  <p className="font-mono text-[10px] uppercase tracking-wider text-[rgba(185,187,211,0.6)]">Closed At</p>
                  <p className="font-mono text-sm text-[#f9f4ed]">
                    {ticket.closedAtUtc ? new Date(ticket.closedAtUtc).toLocaleString() : '-'}
                  </p>
                </div>
              </div>

              <div className="flex flex-wrap gap-2 pt-1">
                <Button
                  color="primary"
                  variant="flat"
                  className="font-mono text-xs uppercase tracking-wider"
                  onPress={takeOwnership}
                  isLoading={actionLoading === 'assign'}
                >
                  Take Ownership
                </Button>
                <Button
                  variant="bordered"
                  className="font-mono text-xs uppercase tracking-wider border-[rgba(106,165,218,0.25)]"
                  onPress={() => changeStatus('Open')}
                  isLoading={actionLoading === 'Open'}
                >
                  Set Open
                </Button>
                <Button
                  variant="bordered"
                  className="font-mono text-xs uppercase tracking-wider border-[rgba(245,158,11,0.3)] text-[#fcd34d]"
                  onPress={() => changeStatus('Assigned')}
                  isLoading={actionLoading === 'Assigned'}
                >
                  Set Assigned
                </Button>
                <Button
                  variant="bordered"
                  className="font-mono text-xs uppercase tracking-wider border-[rgba(34,197,94,0.3)] text-[#86efac]"
                  onPress={() => changeStatus('Closed')}
                  isLoading={actionLoading === 'Closed'}
                >
                  Set Closed
                </Button>
              </div>
            </div>
          </div>

          <div className="rounded-xl border border-[rgba(106,165,218,0.15)] bg-[rgba(36,33,48,0.7)] backdrop-blur-md p-4 space-y-3">
            <h2 className="font-mono text-xs uppercase tracking-wider text-[#6aa5da]">Message</h2>
            <div className="rounded-lg border border-[rgba(106,165,218,0.12)] bg-[rgba(31,28,42,0.7)] px-4 py-3">
              <p className="font-mono text-sm leading-6 whitespace-pre-wrap text-[#f9f4ed]">
                {ticket.message || '(empty)'}
              </p>
            </div>
            <p className="font-mono text-[10px] uppercase tracking-wider text-[rgba(185,187,211,0.5)]">
              Last updated {new Date(ticket.lastUpdatedAtUtc).toLocaleString()}
            </p>
          </div>
        </div>
      )}
    </div>
  )
}
