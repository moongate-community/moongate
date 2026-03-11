import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Chip, Spinner, Table, TableBody, TableCell, TableColumn, TableHeader, TableRow } from '@heroui/react'
import { FormattedMessage, useIntl } from 'react-intl'
import { portalApi } from '../api/portalClient'
import { usePortalAuthStore } from '../store/portalAuthStore'

interface PortalCharacter {
  characterId: string
  name: string
  mapId: number
  mapName: string
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
  const intl = useIntl()
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

  return (
    <div
      className="mx-auto flex w-full max-w-[1180px] flex-col gap-6 px-6 py-8 animate-fade-in"
      style={{
        background: [
          'radial-gradient(circle at top, rgba(214,179,106,0.07), transparent 24%)',
          'linear-gradient(180deg, rgba(24,18,13,0.96) 0%, rgba(30,22,16,0.96) 100%)',
        ].join(', '),
        borderRadius: '24px',
      }}
    >
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p className="mb-2 font-mono text-[11px] uppercase tracking-[0.35em]" style={{ color: 'rgba(249,244,237,0.45)' }}>
            {intl.formatMessage({ id: 'portal.account.portalLabel' })}
          </p>
          <h1 className="font-cinzel text-3xl font-semibold" style={{ color: '#f4ead7' }}>
            {intl.formatMessage({ id: 'portal.account.title' })}
          </h1>
          <p className="mt-3 font-mono text-xs leading-6" style={{ color: 'rgba(244,234,215,0.72)' }}>
            {intl.formatMessage({ id: 'portal.account.subtitle' })}
          </p>
        </div>
      </div>

      {error && (
        <div className="rounded-lg border px-4 py-3" style={{ borderColor: 'rgba(211,98,78,0.28)', background: 'rgba(149,44,34,0.16)' }}>
          <p className="font-mono text-xs uppercase tracking-wider" style={{ color: '#f0b0a4' }}>
            {intl.formatMessage({ id: 'portal.account.errorPrefix' })}: {error}
          </p>
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-[320px_minmax(0,1fr)]">
        <section
          className="rounded-xl border p-6"
          style={{
            background: 'linear-gradient(180deg, rgba(50,36,24,0.9), rgba(34,25,18,0.92))',
            borderColor: 'rgba(214,179,106,0.16)',
            boxShadow: '0 24px 48px rgba(0,0,0,0.26)',
          }}
        >
          <div className="mb-5 flex items-center justify-between gap-3">
            <h2 className="font-cinzel text-lg font-semibold" style={{ color: '#f9f4ed' }}>
              {intl.formatMessage({ id: 'portal.account.section.account' })}
            </h2>
            <Chip
              variant="flat"
              className="font-mono text-[11px] uppercase tracking-[0.16em]"
              style={{ background: 'rgba(214,179,106,0.12)', color: '#f4d6a0' }}
            >
              {account?.accountType ?? user?.role ?? intl.formatMessage({ id: 'portal.account.unknown' })}
            </Chip>
          </div>

          {loading ? (
            <div className="flex min-h-[180px] items-center justify-center">
              <Spinner color="warning" />
            </div>
          ) : (
            <dl className="space-y-4 font-mono text-sm">
              <div>
                <dt className="mb-1 text-[11px] uppercase tracking-[0.18em] text-[rgba(185,187,211,0.48)]">{intl.formatMessage({ id: 'portal.account.field.username' })}</dt>
                <dd style={{ color: '#f4ead7' }}>{account?.username ?? '-'}</dd>
              </div>
              <div>
                <dt className="mb-1 text-[11px] uppercase tracking-[0.18em] text-[rgba(185,187,211,0.48)]">{intl.formatMessage({ id: 'portal.account.field.email' })}</dt>
                <dd style={{ color: '#f4ead7' }}>{account?.email ?? '-'}</dd>
              </div>
              <div>
                <dt className="mb-1 text-[11px] uppercase tracking-[0.18em] text-[rgba(185,187,211,0.48)]">{intl.formatMessage({ id: 'portal.account.field.accountId' })}</dt>
                <dd style={{ color: '#f4ead7' }}>{account?.accountId ?? '-'}</dd>
              </div>
              <div>
                <dt className="mb-1 text-[11px] uppercase tracking-[0.18em] text-[rgba(185,187,211,0.48)]">{intl.formatMessage({ id: 'portal.account.field.characters' })}</dt>
                <dd style={{ color: '#f4ead7' }}>{account?.characters.length ?? 0}</dd>
              </div>
            </dl>
          )}
        </section>

        <section
          className="rounded-xl border p-3"
          style={{
            background: 'linear-gradient(180deg, rgba(39,29,21,0.9), rgba(27,21,16,0.92))',
            borderColor: 'rgba(214,179,106,0.14)',
            boxShadow: '0 24px 48px rgba(0,0,0,0.25)',
          }}
        >
          <div className="mb-3 flex items-center justify-between gap-3 px-3 pt-2">
            <div>
              <h2 className="font-cinzel text-lg font-semibold" style={{ color: '#f9f4ed' }}>
                {intl.formatMessage({ id: 'portal.account.section.characters' })}
              </h2>
              <p className="font-mono text-[11px] uppercase tracking-[0.18em] text-[rgba(185,187,211,0.48)]">
                {intl.formatMessage({ id: 'portal.account.snapshotOnly' })}
              </p>
            </div>
            <Chip
              variant="flat"
              className="font-mono text-[11px] uppercase tracking-[0.16em]"
              style={{ background: 'rgba(214,179,106,0.12)', color: '#f4d6a0' }}
            >
              <FormattedMessage id="portal.account.total" values={{ count: account?.characters.length ?? 0 }} />
            </Chip>
          </div>

          <Table
            aria-label="Portal characters table"
            classNames={{
              wrapper: 'bg-transparent shadow-none',
              th: 'bg-[rgba(214,179,106,0.08)] text-[#f4d6a0] font-mono text-xs tracking-widest uppercase border-b border-[rgba(214,179,106,0.12)]',
              td: 'border-b border-[rgba(214,179,106,0.05)] py-3 font-mono text-sm',
            }}
          >
            <TableHeader>
              <TableColumn>{intl.formatMessage({ id: 'portal.account.table.name' })}</TableColumn>
              <TableColumn>{intl.formatMessage({ id: 'portal.account.table.id' })}</TableColumn>
              <TableColumn>{intl.formatMessage({ id: 'portal.account.table.map' })}</TableColumn>
              <TableColumn>X</TableColumn>
              <TableColumn>Y</TableColumn>
            </TableHeader>
            <TableBody
              items={account?.characters ?? []}
              isLoading={loading}
              loadingContent={<Spinner color="warning" />}
              emptyContent={error ? intl.formatMessage({ id: 'portal.account.loadError' }) : intl.formatMessage({ id: 'portal.account.empty' })}
            >
              {(character) => (
                <TableRow key={character.characterId}>
                  <TableCell>
                    <span style={{ color: '#f4ead7' }}>{character.name || '-'}</span>
                  </TableCell>
                  <TableCell>
                    <span className="text-[rgba(244,234,215,0.72)]">#{character.characterId}</span>
                  </TableCell>
                  <TableCell>
                    <Chip
                      size="sm"
                      variant="flat"
                      className="font-mono text-xs"
                      style={{ background: 'rgba(214,179,106,0.12)', color: '#f4d6a0' }}
                    >
                      {character.mapName || `Map ${character.mapId}`}
                    </Chip>
                  </TableCell>
                  <TableCell>
                    <span className="text-[rgba(244,234,215,0.72)]">{character.x}</span>
                  </TableCell>
                  <TableCell>
                    <span className="text-[rgba(244,234,215,0.72)]">{character.y}</span>
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
