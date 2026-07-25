import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import type { MapFacetInfo } from './maps'
import {
  directionDegrees,
  ONLINE_PLAYERS_QUERY_KEY,
  ONLINE_PLAYERS_REFETCH_MS,
  onlinePlayersQueryOptions,
  playersForFacet,
  type OnlinePlayer,
  useOnlinePlayers,
} from './online-players'

const felucca: MapFacetInfo = {
  name: 'Felucca',
  width: 6144,
  height: 4096,
  maxZoom: 5,
  tileSize: 256,
  tilesAcross: 24,
  tilesDown: 16,
}

const freydis: OnlinePlayer = {
  characterSerial: '0x00000042',
  characterName: 'Freydis',
  accountSerial: '0x00000001',
  accountUsername: 'alice',
  mapId: 0,
  mapName: 'Felucca',
  x: 1420,
  y: 1698,
  z: 10,
  direction: 'SouthEast',
  running: false,
  body: 401,
  skinHue: 1002,
  hits: 48,
  hitsMax: 70,
  warmode: false,
}

function queryWrapper() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  )
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  })
}

describe('online players data module', () => {
  beforeEach(() => vi.restoreAllMocks())

  it.each([
    ['North', 0],
    ['NorthEast', 45],
    ['East', 90],
    ['SouthEast', 135],
    ['South', 180],
    ['SouthWest', 225],
    ['West', 270],
    ['NorthWest', 315],
    ['Unexpected', 0],
  ])('maps %s to %i degrees', (direction, expected) => {
    expect(directionDegrees(direction)).toBe(expected)
  })

  it('keeps only valid players on the selected facet, case-insensitively', () => {
    const players: OnlinePlayer[] = [
      freydis,
      { ...freydis, characterSerial: '0x43', mapName: 'felucca', x: 0, y: 0 },
      { ...freydis, characterSerial: '0x44', mapName: 'Ilshenar' },
      { ...freydis, characterSerial: '0x45', x: -1 },
      { ...freydis, characterSerial: '0x46', x: felucca.width },
      { ...freydis, characterSerial: '0x47', y: felucca.height },
    ]

    expect(playersForFacet(players, felucca).map((player) => player.characterSerial)).toEqual(['0x00000042', '0x43'])
  })

  it('describes five-second foreground polling only while enabled', () => {
    expect(ONLINE_PLAYERS_QUERY_KEY).toEqual(['admin', 'players', 'online'])
    expect(ONLINE_PLAYERS_REFETCH_MS).toBe(5000)
    expect(onlinePlayersQueryOptions(true)).toMatchObject({
      queryKey: ONLINE_PLAYERS_QUERY_KEY,
      enabled: true,
      refetchInterval: 5000,
      refetchIntervalInBackground: false,
      refetchOnWindowFocus: true,
    })
    expect(onlinePlayersQueryOptions(false)).toMatchObject({
      enabled: false,
      refetchInterval: false,
    })
  })

  it('does not request the admin endpoint while disabled', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(json([]))

    const { result } = renderHook(() => useOnlinePlayers(false), {
      wrapper: queryWrapper(),
    })

    await Promise.resolve()
    expect(result.current.fetchStatus).toBe('idle')
    expect(fetchSpy).not.toHaveBeenCalled()
  })

  it('reads the online-player snapshot while enabled', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(json([freydis]))

    const { result } = renderHook(() => useOnlinePlayers(true), {
      wrapper: queryWrapper(),
    })

    await waitFor(() => expect(result.current.data).toEqual([freydis]))
    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/v1/admin/players/online',
      expect.objectContaining({ headers: expect.any(Headers) }),
    )
  })
})
