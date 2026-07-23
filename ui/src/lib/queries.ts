import { useQuery } from '@tanstack/react-query'
import { apiFetch } from './api'
import type { components } from './api-types'

// Aliased from the generated schemas rather than re-declared. Hand-writing these would throw away the
// reason for generating them: a DTO changing shape has to break compilation here, not surface as an
// undefined in the browser.
export type PlayerMe = components['schemas']['PlayerMeResponse']
export type Character = components['schemas']['CharacterResponse']
export type ServerStats = components['schemas']['ServerStatsResponse']

export const useMe = () =>
  useQuery({ queryKey: ['me'], queryFn: () => apiFetch<PlayerMe>('/api/v1/player/me') })

export const useMyCharacters = () =>
  useQuery({
    queryKey: ['me', 'characters'],
    queryFn: () => apiFetch<Character[]>('/api/v1/player/me/characters'),
  })

export const useStats = () =>
  useQuery({ queryKey: ['stats'], queryFn: () => apiFetch<ServerStats>('/api/v1/stats') })

export type ServerVersion = components['schemas']['VersionResponse']

export const useVersion = () =>
  useQuery({ queryKey: ['version'], queryFn: () => apiFetch<ServerVersion>('/api/v1/version') })
