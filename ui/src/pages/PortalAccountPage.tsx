import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Chip, Spinner, Table, TableBody, TableCell, TableColumn, TableHeader, TableRow } from '@heroui/react'
import { FormattedMessage, useIntl } from 'react-intl'
import { portalApi, rawPortalApiFetch } from '../api/portalClient'
import { ItemImageHoverPreview } from '../components/ItemImageHoverPreview'
import { usePortalAuthStore } from '../store/portalAuthStore'

interface PortalCharacter {
  characterId: string
  name: string
  mapId: number
  mapName: string
  x: number
  y: number
}

interface PortalInventoryItem {
  itemId: string
  serial: string
  name: string
  graphic: number
  hue: number
  amount: number
  location: string
  layer?: string | null
  containerSerial?: string | null
  containerName?: string | null
  imageUrl: string
}

interface PortalInventory {
  characterId: string
  characterName: string
  items: PortalInventoryItem[]
  bankItems: PortalInventoryItem[]
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
  const [selectedCharacterId, setSelectedCharacterId] = useState<string | null>(null)
  const [inventoryLoading, setInventoryLoading] = useState(false)
  const [inventoryError, setInventoryError] = useState<string | null>(null)
  const [inventory, setInventory] = useState<PortalInventory | null>(null)

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
          setSelectedCharacterId((current) => current ?? payload.characters[0]?.characterId ?? null)
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

  useEffect(() => {
    if (!selectedCharacterId) {
      setInventory(null)
      setInventoryError(null)
      return
    }

    let mounted = true

    async function loadInventory() {
      setInventoryLoading(true)
      setInventoryError(null)

      try {
        const payload = await portalApi.get<PortalInventory>(`/portal/characters/${selectedCharacterId}/inventory`)
        if (mounted) {
          setInventory(payload)
        }
      } catch (loadError) {
        if (mounted) {
          const message = loadError instanceof Error ? loadError.message : 'Failed to load inventory.'
          setInventoryError(message)
          setInventory(null)
        }
      } finally {
        if (mounted) {
          setInventoryLoading(false)
        }
      }
    }

    void loadInventory()

    return () => {
      mounted = false
    }
  }, [selectedCharacterId])

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
                <TableRow
                  key={character.characterId}
                  className="cursor-pointer"
                  onClick={() => setSelectedCharacterId(character.characterId)}
                >
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
                    <div className="flex items-center justify-between gap-3">
                      <span className="text-[rgba(244,234,215,0.72)]">{character.y}</span>
                      {selectedCharacterId === character.characterId && (
                        <Chip
                          size="sm"
                          variant="flat"
                          className="font-mono text-[10px] uppercase tracking-[0.16em]"
                          style={{ background: 'rgba(214,179,106,0.12)', color: '#f4d6a0' }}
                        >
                          {intl.formatMessage({ id: 'portal.account.selected' })}
                        </Chip>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </section>
      </div>

      <section
        className="rounded-xl border p-3"
        style={{
          background: 'linear-gradient(180deg, rgba(31,24,18,0.92), rgba(21,17,13,0.94))',
          borderColor: 'rgba(214,179,106,0.14)',
          boxShadow: '0 24px 48px rgba(0,0,0,0.24)',
        }}
      >
        <div className="mb-3 flex flex-wrap items-center justify-between gap-3 px-3 pt-2">
          <div>
            <h2 className="font-cinzel text-lg font-semibold" style={{ color: '#f9f4ed' }}>
              {intl.formatMessage({ id: 'portal.account.section.inventory' })}
            </h2>
            <p className="font-mono text-[11px] uppercase tracking-[0.18em] text-[rgba(185,187,211,0.48)]">
              {inventory?.characterName
                ? intl.formatMessage({ id: 'portal.account.inventoryFor' }, { name: inventory.characterName })
                : intl.formatMessage({ id: 'portal.account.selectCharacter' })}
            </p>
          </div>
          <Chip
            variant="flat"
            className="font-mono text-[11px] uppercase tracking-[0.16em]"
            style={{ background: 'rgba(214,179,106,0.12)', color: '#f4d6a0' }}
          >
            <FormattedMessage id="portal.account.total" values={{ count: inventory?.items.length ?? 0 }} />
          </Chip>
        </div>

        {inventoryError && (
          <div className="mx-3 mb-3 rounded-lg border px-4 py-3" style={{ borderColor: 'rgba(211,98,78,0.28)', background: 'rgba(149,44,34,0.16)' }}>
            <p className="font-mono text-xs uppercase tracking-wider" style={{ color: '#f0b0a4' }}>
              {intl.formatMessage({ id: 'portal.account.errorPrefix' })}: {inventoryError}
            </p>
          </div>
        )}

        <Table
          aria-label="Portal inventory table"
          classNames={{
            wrapper: 'bg-transparent shadow-none',
            th: 'bg-[rgba(214,179,106,0.08)] text-[#f4d6a0] font-mono text-xs tracking-widest uppercase border-b border-[rgba(214,179,106,0.12)]',
            td: 'border-b border-[rgba(214,179,106,0.05)] py-3 font-mono text-sm align-middle',
          }}
        >
          <TableHeader>
            <TableColumn>{intl.formatMessage({ id: 'portal.account.inventory.image' })}</TableColumn>
            <TableColumn>{intl.formatMessage({ id: 'portal.account.inventory.name' })}</TableColumn>
            <TableColumn>{intl.formatMessage({ id: 'portal.account.inventory.serial' })}</TableColumn>
            <TableColumn>{intl.formatMessage({ id: 'portal.account.inventory.itemId' })}</TableColumn>
            <TableColumn>{intl.formatMessage({ id: 'portal.account.inventory.amount' })}</TableColumn>
            <TableColumn>{intl.formatMessage({ id: 'portal.account.inventory.location' })}</TableColumn>
          </TableHeader>
          <TableBody
            items={inventory?.items ?? []}
            isLoading={inventoryLoading}
            loadingContent={<Spinner color="warning" />}
            emptyContent={
              selectedCharacterId
                ? intl.formatMessage({ id: 'portal.account.inventory.empty' })
                : intl.formatMessage({ id: 'portal.account.selectCharacter' })
            }
          >
            {(item) => (
              <TableRow key={item.serial}>
                <TableCell>
                  <div
                    className="flex h-12 w-12 items-center justify-center overflow-hidden rounded-lg border"
                    style={{ borderColor: 'rgba(214,179,106,0.12)', background: 'rgba(214,179,106,0.05)' }}
                  >
                    <InventoryItemImage imageUrl={item.imageUrl} name={item.name} />
                  </div>
                </TableCell>
                <TableCell>
                  <div className="flex flex-col gap-1">
                    <span style={{ color: '#f4ead7' }}>{item.name}</span>
                    {item.layer && (
                      <span className="text-[11px] uppercase tracking-[0.16em] text-[rgba(185,187,211,0.52)]">{item.layer}</span>
                    )}
                  </div>
                </TableCell>
                <TableCell>
                  <span className="text-[rgba(244,234,215,0.72)]">#{item.serial}</span>
                </TableCell>
                <TableCell>
                  <span className="text-[rgba(244,234,215,0.72)]">{item.itemId}</span>
                </TableCell>
                <TableCell>
                  <span className="text-[rgba(244,234,215,0.72)]">{item.amount}</span>
                </TableCell>
                <TableCell>
                  <Chip
                    size="sm"
                    variant="flat"
                    className="font-mono text-xs"
                    style={{ background: 'rgba(214,179,106,0.12)', color: '#f4d6a0' }}
                  >
                    {item.location}
                  </Chip>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </section>

      <section
        className="rounded-xl border p-3"
        style={{
          background: 'linear-gradient(180deg, rgba(28,22,18,0.94), rgba(18,14,12,0.96))',
          borderColor: 'rgba(214,179,106,0.14)',
          boxShadow: '0 24px 48px rgba(0,0,0,0.24)',
        }}
      >
        <div className="mb-3 flex flex-wrap items-center justify-between gap-3 px-3 pt-2">
          <div>
            <h2 className="font-cinzel text-lg font-semibold" style={{ color: '#f9f4ed' }}>
              {intl.formatMessage({ id: 'portal.account.section.bank' })}
            </h2>
            <p className="font-mono text-[11px] uppercase tracking-[0.18em] text-[rgba(185,187,211,0.48)]">
              {inventory?.characterName
                ? intl.formatMessage({ id: 'portal.account.bankFor' }, { name: inventory.characterName })
                : intl.formatMessage({ id: 'portal.account.selectCharacter' })}
            </p>
          </div>
          <Chip
            variant="flat"
            className="font-mono text-[11px] uppercase tracking-[0.16em]"
            style={{ background: 'rgba(214,179,106,0.12)', color: '#f4d6a0' }}
          >
            <FormattedMessage id="portal.account.total" values={{ count: inventory?.bankItems.length ?? 0 }} />
          </Chip>
        </div>

        <Table
          aria-label="Portal bank table"
          classNames={{
            wrapper: 'bg-transparent shadow-none',
            th: 'bg-[rgba(214,179,106,0.08)] text-[#f4d6a0] font-mono text-xs tracking-widest uppercase border-b border-[rgba(214,179,106,0.12)]',
            td: 'border-b border-[rgba(214,179,106,0.05)] py-3 font-mono text-sm align-middle',
          }}
        >
          <TableHeader>
            <TableColumn>{intl.formatMessage({ id: 'portal.account.inventory.image' })}</TableColumn>
            <TableColumn>{intl.formatMessage({ id: 'portal.account.inventory.name' })}</TableColumn>
            <TableColumn>{intl.formatMessage({ id: 'portal.account.inventory.serial' })}</TableColumn>
            <TableColumn>{intl.formatMessage({ id: 'portal.account.inventory.itemId' })}</TableColumn>
            <TableColumn>{intl.formatMessage({ id: 'portal.account.inventory.amount' })}</TableColumn>
            <TableColumn>{intl.formatMessage({ id: 'portal.account.inventory.location' })}</TableColumn>
          </TableHeader>
          <TableBody
            items={inventory?.bankItems ?? []}
            isLoading={inventoryLoading}
            loadingContent={<Spinner color="warning" />}
            emptyContent={
              selectedCharacterId
                ? intl.formatMessage({ id: 'portal.account.bank.empty' })
                : intl.formatMessage({ id: 'portal.account.selectCharacter' })
            }
          >
            {(item) => (
              <TableRow key={`bank-${item.serial}`}>
                <TableCell>
                  <div
                    className="flex h-12 w-12 items-center justify-center overflow-hidden rounded-lg border"
                    style={{ borderColor: 'rgba(214,179,106,0.12)', background: 'rgba(214,179,106,0.05)' }}
                  >
                    <InventoryItemImage imageUrl={item.imageUrl} name={item.name} />
                  </div>
                </TableCell>
                <TableCell>
                  <div className="flex flex-col gap-1">
                    <span style={{ color: '#f4ead7' }}>{item.name}</span>
                    {item.layer && (
                      <span className="text-[11px] uppercase tracking-[0.16em] text-[rgba(185,187,211,0.52)]">{item.layer}</span>
                    )}
                  </div>
                </TableCell>
                <TableCell>
                  <span className="text-[rgba(244,234,215,0.72)]">#{item.serial}</span>
                </TableCell>
                <TableCell>
                  <span className="text-[rgba(244,234,215,0.72)]">{item.itemId}</span>
                </TableCell>
                <TableCell>
                  <span className="text-[rgba(244,234,215,0.72)]">{item.amount}</span>
                </TableCell>
                <TableCell>
                  <Chip
                    size="sm"
                    variant="flat"
                    className="font-mono text-xs"
                    style={{ background: 'rgba(214,179,106,0.12)', color: '#f4d6a0' }}
                  >
                    {item.location}
                  </Chip>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </section>
    </div>
  )
}

function InventoryItemImage({ imageUrl, name }: { imageUrl: string; name: string }) {
  const [src, setSrc] = useState<string | null>(null)

  useEffect(() => {
    let mounted = true
    let objectUrl: string | null = null

    async function loadImage() {
      try {
        const response = await rawPortalApiFetch(imageUrl)
        const blob = await response.blob()
        objectUrl = URL.createObjectURL(blob)

        if (mounted) {
          setSrc(objectUrl)
        }
      } catch {
        if (mounted) {
          setSrc(null)
        }
      }
    }

    void loadImage()

    return () => {
      mounted = false

      if (objectUrl) {
        URL.revokeObjectURL(objectUrl)
      }
    }
  }, [imageUrl])

  if (!src) {
    return <span className="font-mono text-[10px] uppercase tracking-[0.14em] text-[rgba(244,234,215,0.4)]">No Art</span>
  }

  const imageNode = (
    <img
      src={src}
      alt={name}
      className="max-h-10 max-w-10 object-contain"
    />
  )

  return (
    <ItemImageHoverPreview previewSrc={src} name={name}>
      {imageNode}
    </ItemImageHoverPreview>
  )
}
