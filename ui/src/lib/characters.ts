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

export type CharacterPage = components['schemas']['CharacterResponsePagedResponse']

/** The server's own default. Named here so the pager and the request cannot disagree. */
export const CHARACTERS_PAGE_SIZE = 25

/**
 * The staff listing URL. A blank search is omitted rather than sent empty: the server reads a blank
 * `search` as "no filter" anyway, and leaving it out keeps the query key stable so paging with an
 * empty box does not miss the cache.
 */
export function charactersQuery({ page, search }: { page: number; search: string }): string {
  const params = new URLSearchParams({
    page: String(Math.max(page, 1)),
    pageSize: String(CHARACTERS_PAGE_SIZE),
  })

  const trimmed = search.trim()

  if (trimmed !== '') {
    params.set('search', trimmed)
  }

  return `/api/v1/admin/characters?${params}`
}

/**
 * Every character on the shard, paged and searched by the server. Staff only.
 *
 * The previous page is held while the next loads: without it the table blanks to its loading state
 * on every keystroke and every page turn, which reads as a slideshow rather than a table.
 */
export const useAllCharacters = (params: { page: number; search: string }) =>
  useQuery({
    queryKey: ['admin', 'characters', params.page, params.search.trim()],
    queryFn: () => apiFetch<CharacterPage>(charactersQuery(params)),
    placeholderData: (previous: CharacterPage | undefined) => previous,
  })

export type CharacterDetail = components['schemas']['CharacterDetailResponse']
export type CharacterItem = components['schemas']['CharacterItemResponse']
export type CharacterSkill = components['schemas']['CharacterSkillResponse']

/** One character in full — your own, or anyone's for staff. The server decides which. */
export const useCharacter = (serial: string) =>
  useQuery({
    queryKey: ['characters', serial],
    queryFn: () => apiFetch<CharacterDetail>(`/api/v1/characters/${serial}`),
  })

/**
 * An item's art. The id is the ART id in hex — not the item's serial — and hue 0 is the raw art, so
 * it is omitted rather than sent: two spellings of one picture would sit in the cache as two.
 * No cache-busting parameter, for the same reason as {@link figureUrl}.
 */
export function itemImageUrl(itemId: number, hue = 0): string {
  const id = `0x${itemId.toString(16)}`

  return hue === 0 ? `/api/v1/images/items/${id}.png` : `/api/v1/images/items/${id}.png?hue=0x${hue.toString(16)}`
}
