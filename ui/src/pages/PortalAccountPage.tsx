import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button, Chip, Spinner, Table, TableBody, TableCell, TableColumn, TableHeader, TableRow } from '@heroui/react'
import { portalApi } from '../api/portalClient'
import { usePortalAuthStore } from '../store/portalAuthStore'

interface PortalCharacter {
  characterId: string
  name: string
  mapId: number
  x: number
  y: number
}

interface PortalAccount {
  accountId: string
  username: string
  email: string
  accountType: string
  characters: PortalCharacter[]
}

export function PortalAccountPage() {
  const navigate = useNavigate()
  const user = usePortalAuthStore((s) => s.user)
  const logout = usePortalAuthStore((s) => s.logout)

  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [account, setAccount] = useState<PortalAccount | null>(null)

  useEffect(() => {
    if (!user) {
      navigate('/portal/login', { replace: true })
      return
    }

    let mounted = true

    async function load() {
      setLoading(true)
      setError(null)

      try {
        const payload = await portalApi.get<PortalAccount>('/portal/me')
        if (mounted) {
          setAccount(payload)
        }
      } catch (loadError) {
        if (mounted) {
          const message = loadError instanceof Error ? loadError.message : 'Failed to load account.'
          setError(message)

          if (message.includes('401') || message.toLowerCase().includes('unauthorized')) {
            logout()
            navigate('/portal/login', { replace: true })
          }
        }
      } finally {
        if (mounted) {
          setLoading(false)
        }
      }
    }

    void load()

    return () => {
      mounted = false
    }
  }, [logout, navigate, user])

  function handleLogout() {
    logout()
    navigate('/portal/login', { replace: true })
  }

  return (
    <div className="mx-auto flex w-full max-w-[1180px] flex-col gap-6 px-6 py-8 animate-fade-in">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p className="mb-2 font-mono text-[11px] uppercase tracking-[0.35em]" style={{ color: 'rgba(249,244,237,0.45)' }}>
            Player Portal
          </p>
          <h1 className="font-cinzel text-3xl font-semibold" style={{ color: '#f4d6a0' }}>
            Account Overview
          </h1>
          <p className="mt-3 font-mono text-xs leading-6" style={{ color: 'rgba(249,244,237,0.62)' }}>
            Read-only character summary. This page is the base for future marketplace flows.
          </p>
        </div>

        <Button
          variant="bordered"
          className="font-mono text-xs uppercase tracking-[0.18em]"
          style={{ borderColor: 'rgba(196,154,94,0.24)', color: '#f4d6a0' }}
          onPress={handleLogout}
        >
          Logout
        </Button>
      </div>

      {error && (
        <div className="rounded-lg border border-[rgba(239,68,68,0.28)] bg-[rgba(239,68,68,0.1)] px-4 py-3">
          <p className="font-mono text-xs uppercase tracking-wider text-[#ef4444]">Error: {error}</p>
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-[320px_minmax(0,1fr)]">
        <section
          className="rounded-xl border p-6"
          style={{
            background: 'rgba(22,24,36,0.76)',
            borderColor: 'rgba(196,154,94,0.16)',
            boxShadow: '0 24px 48px rgba(0,0,0,0.25)',
          }}
        >
          <div className="mb-5 flex items-center justify-between gap-3">
            <h2 className="font-cinzel text-lg font-semibold" style={{ color: '#f9f4ed' }}>
              Account
            </h2>
            <Chip
              variant="flat"
              className="font-mono text-[11px] uppercase tracking-[0.16em]"
              style={{ background: 'rgba(196,154,94,0.12)', color: '#f4d6a0' }}
            >
              {account?.accountType ?? user?.role ?? 'Unknown'}
            </Chip>
          </div>

          {loading ? (
            <div className="flex min-h-[180px] items-center justify-center">
              <Spinner color="warning" />
            </div>
          ) : (
            <dl className="space-y-4 font-mono text-sm">
              <div>
                <dt className="mb-1 text-[11px] uppercase tracking-[0.18em] text-[rgba(185,187,211,0.48)]">Username</dt>
                <dd style={{ color: '#f9f4ed' }}>{account?.username ?? '-'}</dd>
              </div>
              <div>
                <dt className="mb-1 text-[11px] uppercase tracking-[0.18em] text-[rgba(185,187,211,0.48)]">Email</dt>
                <dd style={{ color: '#f9f4ed' }}>{account?.email ?? '-'}</dd>
              </div>
              <div>
                <dt className="mb-1 text-[11px] uppercase tracking-[0.18em] text-[rgba(185,187,211,0.48)]">Account Id</dt>
                <dd style={{ color: '#f9f4ed' }}>{account?.accountId ?? '-'}</dd>
              </div>
              <div>
                <dt className="mb-1 text-[11px] uppercase tracking-[0.18em] text-[rgba(185,187,211,0.48)]">Characters</dt>
                <dd style={{ color: '#f9f4ed' }}>{account?.characters.length ?? 0}</dd>
              </div>
            </dl>
          )}
        </section>

        <section
          className="rounded-xl border p-3"
          style={{
            background: 'rgba(36,33,48,0.72)',
            borderColor: 'rgba(196,154,94,0.14)',
            boxShadow: '0 24px 48px rgba(0,0,0,0.25)',
          }}
        >
          <div className="mb-3 flex items-center justify-between gap-3 px-3 pt-2">
            <div>
              <h2 className="font-cinzel text-lg font-semibold" style={{ color: '#f9f4ed' }}>
                Characters
              </h2>
              <p className="font-mono text-[11px] uppercase tracking-[0.18em] text-[rgba(185,187,211,0.48)]">
                Snapshot only
              </p>
            </div>
            <Chip
              variant="flat"
              className="font-mono text-[11px] uppercase tracking-[0.16em]"
              style={{ background: 'rgba(196,154,94,0.12)', color: '#f4d6a0' }}
            >
              Total: {account?.characters.length ?? 0}
            </Chip>
          </div>

          <Table
            aria-label="Portal characters table"
            classNames={{
              wrapper: 'bg-transparent shadow-none',
              th: 'bg-[rgba(196,154,94,0.08)] text-[#f4d6a0] font-mono text-xs tracking-widest uppercase border-b border-[rgba(196,154,94,0.12)]',
              td: 'border-b border-[rgba(196,154,94,0.05)] py-3 font-mono text-sm',
            }}
          >
            <TableHeader>
              <TableColumn>NAME</TableColumn>
              <TableColumn>ID</TableColumn>
              <TableColumn>MAP</TableColumn>
              <TableColumn>X</TableColumn>
              <TableColumn>Y</TableColumn>
            </TableHeader>
            <TableBody
              items={account?.characters ?? []}
              isLoading={loading}
              loadingContent={<Spinner color="warning" />}
              emptyContent={error ? 'Unable to load characters.' : 'No characters found.'}
            >
              {(character) => (
                <TableRow key={character.characterId}>
                  <TableCell>
                    <span style={{ color: '#f9f4ed' }}>{character.name || '-'}</span>
                  </TableCell>
                  <TableCell>
                    <span className="text-[rgba(185,187,211,0.8)]">#{character.characterId}</span>
                  </TableCell>
                  <TableCell>
                    <Chip
                      size="sm"
                      variant="flat"
                      className="font-mono text-xs"
                      style={{ background: 'rgba(196,154,94,0.12)', color: '#f4d6a0' }}
                    >
                      {character.mapId}
                    </Chip>
                  </TableCell>
                  <TableCell>
                    <span className="text-[rgba(185,187,211,0.8)]">{character.x}</span>
                  </TableCell>
                  <TableCell>
                    <span className="text-[rgba(185,187,211,0.8)]">{character.y}</span>
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </section>
      </div>
    </div>
  )
}
