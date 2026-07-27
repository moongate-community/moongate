import { useQuery } from '@tanstack/react-query'
import { apiFetch } from './api'
import type { components } from './api-types'
import type { MapFacetInfo } from './maps'

export type OnlinePlayer = components['schemas']['OnlinePlayerMapResponse']

export const ONLINE_PLAYERS_QUERY_KEY = ['admin', 'players', 'online'] as const
export const ONLINE_PLAYERS_REFETCH_MS = 5000

export function directionDegrees(direction: string): number {
  switch (direction) {
    case 'NorthEast':
      return 45
    case 'East':
      return 90
    case 'SouthEast':
      return 135
    case 'South':
      return 180
    case 'SouthWest':
      return 225
    case 'West':
      return 270
    case 'NorthWest':
      return 315
    default:
      return 0
  }
}

export function playersForFacet(players: readonly OnlinePlayer[], facet: MapFacetInfo): OnlinePlayer[] {
  const facetName = facet.name.toLowerCase()

  return players.filter(
    (player) =>
      player.mapName.toLowerCase() === facetName &&
      player.x >= 0 &&
      player.x < facet.width &&
      player.y >= 0 &&
      player.y < facet.height,
  )
}

export function onlinePlayersQueryOptions(enabled: boolean) {
  return {
    queryKey: ONLINE_PLAYERS_QUERY_KEY,
    queryFn: () => apiFetch<OnlinePlayer[]>('/api/v1/admin/players/online'),
    enabled,
    refetchInterval: enabled ? ONLINE_PLAYERS_REFETCH_MS : false,
    refetchIntervalInBackground: false,
    refetchOnWindowFocus: true,
  } as const
}

export const useOnlinePlayers = (enabled: boolean) => useQuery(onlinePlayersQueryOptions(enabled))
