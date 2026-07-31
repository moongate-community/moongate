import { useQuery } from '@tanstack/react-query'

import { apiFetch } from './api'
import type { components } from './api-types'

export type Character = components['schemas']['CharacterResponse']

const MINE_KEY = ['player', 'characters']

/** The caller's own characters. The API fills in the account, so there is nothing to pass. */
export const useMyCharacters = () =>
  useQuery({ queryKey: MINE_KEY, queryFn: () => apiFetch<Character[]>('/api/v1/player/me/characters') })

/**
 * The in-world figure: body, hair and what the character is wearing.
 *
 * Deliberately no cache-busting parameter. The URL is stable and the ETag is a fingerprint of the
 * appearance, so the browser revalidates and takes a 304 until the character changes clothes.
 * Appending `?t=` would force a full download on every visit — the opposite of what the route is
 * built for.
 */
export const figureUrl = (serial: string) => `/api/v1/images/mobiles/${serial}.png`

/** The paperdoll, with the classic backdrop unless it is turned off. Cached like {@link figureUrl}. */
export const paperdollUrl = (serial: string, background = true) =>
  background
    ? `/api/v1/images/mobiles/${serial}/paperdoll.png`
    : `/api/v1/images/mobiles/${serial}/paperdoll.png?background=false`
