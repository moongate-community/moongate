import { useEffect, useState } from 'react'
import { Chip, Spinner, Table, TableBody, TableCell, TableColumn, TableHeader, TableRow } from '@heroui/react'
import { api } from '../api/client'

interface ActiveSession {
  sessionId: number
  accountId: string
  username: string
  accountType: string
  characterId: string
  characterName: string
}

export function ActivePlayersPage() {
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [sessions, setSessions] = useState<ActiveSession[]>([])

  useEffect(() => {
    let mounted = true

    async function load() {
      setLoading(true)
      setError(null)

      try {
        const payload = await api.get<ActiveSession[]>('/sessions/active')
        if (mounted) {
          setSessions(payload)
        }
      } catch (fetchError) {
        if (mounted) {
          setError(fetchError instanceof Error ? fetchError.message : 'Failed to load active sessions.')
        }
      } finally {
        if (mounted) {
          setLoading(false)
        }
      }
    }

    load()
    const intervalId = window.setInterval(load, 5000)

    return () => {
      mounted = false
      window.clearInterval(intervalId)
    }
  }, [])

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
              Active Players
            </h1>
          </div>
          <p className="font-mono text-xs pl-5" style={{ color: 'rgba(185,187,211,0.35)', letterSpacing: '0.1em' }}>
            LIVE IN-GAME SESSIONS
          </p>
        </div>

        <Chip
          variant="flat"
          className="font-mono text-xs tracking-wider uppercase"
          style={{
            background: 'rgba(106,165,218,0.1)',
            border: '1px solid rgba(106,165,218,0.2)',
            color: '#6aa5da',
          }}
        >
          Online: {sessions.length}
        </Chip>
      </div>

      {error && (
        <div className="rounded-lg border border-[rgba(239,68,68,0.28)] bg-[rgba(239,68,68,0.1)] px-4 py-3">
          <p className="font-mono text-xs uppercase tracking-wider text-[#ef4444]">Error: {error}</p>
        </div>
      )}

      <div className="rounded-xl border border-[rgba(106,165,218,0.15)] bg-[rgba(36,33,48,0.7)] backdrop-blur-md p-2">
        <Table
          aria-label="Active players table"
          classNames={{
            wrapper: 'bg-transparent shadow-none',
            th: 'bg-[rgba(106,165,218,0.1)] text-[#6aa5da] font-mono text-xs tracking-widest uppercase border-b border-[rgba(106,165,218,0.15)]',
            td: 'border-b border-[rgba(106,165,218,0.05)] py-3 font-mono text-sm',
          }}
        >
          <TableHeader>
            <TableColumn>SESSION</TableColumn>
            <TableColumn>PLAYER</TableColumn>
            <TableColumn>CHARACTER</TableColumn>
            <TableColumn>LEVEL</TableColumn>
            <TableColumn>ACCOUNT ID</TableColumn>
          </TableHeader>
          <TableBody
            items={sessions}
            isLoading={loading}
            loadingContent={<Spinner color="primary" />}
            emptyContent={error ? 'Unable to load active players.' : 'No active players in game.'}
          >
            {(session) => (
              <TableRow key={session.sessionId}>
                <TableCell>
                  <span className="text-[rgba(185,187,211,0.9)]">{session.sessionId}</span>
                </TableCell>
                <TableCell>
                  <span className="text-[#f9f4ed]">{session.username || '-'}</span>
                </TableCell>
                <TableCell>
                  <div className="flex flex-col gap-0.5">
                    <span className="text-[#6aa5da]">{session.characterName || '-'}</span>
                    <span className="text-[rgba(185,187,211,0.6)] text-xs">#{session.characterId || '-'}</span>
                  </div>
                </TableCell>
                <TableCell>
                  <Chip
                    size="sm"
                    variant="flat"
                    className="font-mono text-xs tracking-wider"
                    style={{
                      background: 'rgba(106,165,218,0.1)',
                      border: '1px solid rgba(106,165,218,0.15)',
                      color: '#f9f4ed',
                    }}
                  >
                    {session.accountType}
                  </Chip>
                </TableCell>
                <TableCell>
                  <span className="text-[rgba(185,187,211,0.9)]">{session.accountId}</span>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>
    </div>
  )
}
