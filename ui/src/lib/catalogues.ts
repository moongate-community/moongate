import { useQuery } from '@tanstack/react-query'

import { apiFetch } from './api'
import type { components } from './api-types'

/**
 * One row of any catalogue, reduced to what a picker draws. Every catalogue answers a different shape
 * — a hue has no art, a tile has no name worth reading — so each one says how its rows become this,
 * and the picker never learns there is more than one kind.
 */
export type CatalogueEntry = {
  /** The value the picker hands back, and the row's React key. */
  value: string
  /** The line a human reads. */
  label: string
  /** The line underneath: an id, a hex, a count. Drawn in the mono face. */
  detail: string
  /** Art for the tile, when the catalogue has any. */
  imageUrl?: string
  /** A flat colour for the tile, for the catalogue that is colours. */
  swatch?: string
}

/** One choice in a catalogue's filter, when it has one. */
export type CatalogueFilter = {
  /** The query parameter the server reads. */
  param: string
  /** Values offered, in order. The first is "no filter" and is sent as absent. */
  values: string[]
}

export type Catalogue = {
  id: string
  path: string
  toEntry: (row: never) => CatalogueEntry
  filter?: CatalogueFilter
}

/** The server's own default page size. Named here so the pager and the request cannot disagree. */
export const CATALOGUE_PAGE_SIZE = 25

/**
 * A catalogue request. A blank search or filter is left out rather than sent empty: the server reads
 * a blank one as "no filter" anyway, and omitting it keeps the query key stable, so paging with an
 * empty box does not miss the cache.
 */
export function cataloguePath(
  path: string,
  { page, search, filter }: { page: number; search: string; filter?: { param: string; value: string } },
): string {
  const params = new URLSearchParams({
    page: String(Math.max(page, 1)),
    pageSize: String(CATALOGUE_PAGE_SIZE),
  })

  const trimmed = search.trim()

  if (trimmed !== '') {
    params.set('search', trimmed)
  }

  if (filter && filter.value !== '') {
    params.set(filter.param, filter.value)
  }

  return `${path}?${params}`
}

/** The shape every catalogue route answers with, whatever it is a catalogue of. */
type ServerPage = { items: unknown[]; total: number; page: number; pageSize: number; totalPages: number }

/**
 * A page, stamped with the catalogue it came from. The stamp is what makes holding the previous page
 * while the next loads safe across a change of catalogue: the rows of one catalogue read through the
 * reader of another are nonsense, and without the stamp they would be drawn for an instant.
 */
export type CataloguePage = ServerPage & { catalogueId: string }

/** One page of any catalogue, paged and searched by the server. Staff only. */
export const useCatalogue = (catalogue: Catalogue, params: { page: number; search: string; filter?: string }) =>
  useQuery({
    queryKey: ['admin', 'catalogue', catalogue.id, params.page, params.search.trim(), params.filter ?? ''],
    queryFn: async () => {
      const page = await apiFetch<ServerPage>(
        cataloguePath(catalogue.path, {
          page: params.page,
          search: params.search,
          filter: catalogue.filter && { param: catalogue.filter.param, value: params.filter ?? '' },
        }),
      )

      return { ...page, catalogueId: catalogue.id }
    },
    // Holding the previous page while the next loads is what makes it read as a list rather than a
    // slideshow.
    placeholderData: (previous: CataloguePage | undefined) => previous,
  })

type Hue = components['schemas']['HueSummary']
type UoItem = components['schemas']['UoItemSummary']
type Body = components['schemas']['BodySummary']
type HairStyle = components['schemas']['HairStyleSummary']
type ItemTemplate = components['schemas']['ItemTemplateSummaryResponse']
type MobileTemplate = components['schemas']['MobileTemplateSummaryResponse']

/**
 * Declares a catalogue. The cast is the one place the picker's single row shape meets six different
 * server shapes; it is contained here so that everything downstream is typed.
 */
function catalogue<TRow>(
  id: string,
  path: string,
  toEntry: (row: TRow) => CatalogueEntry,
  filter?: CatalogueFilter,
): Catalogue {
  return { id, path, toEntry: toEntry as (row: never) => CatalogueEntry, filter }
}

/** Every TileFlagType worth filtering by. The blank first entry means "no filter". */
const TILE_FLAGS = ['', 'Container', 'Wearable', 'Weapon', 'Armor', 'Surface', 'Impassable', 'Generic']

/**
 * The six catalogues the shard can be browsed by. Adding a seventh is one entry here — the picker
 * itself has nothing to learn.
 */
export const CATALOGUES: Catalogue[] = [
  catalogue<ItemTemplate>('itemTemplates', '/api/v1/admin/items/templates', (row) => ({
    value: row.id,
    label: row.name,
    detail: row.id,
    imageUrl: row.imageUrl,
  })),
  catalogue<MobileTemplate>('mobileTemplates', '/api/v1/admin/mobiles/templates', (row) => ({
    value: row.id,
    label: row.name,
    detail: row.id,
    imageUrl: row.imageUrl,
  })),
  catalogue<UoItem>(
    'uoItems',
    '/api/v1/admin/uo-items',
    (row) => ({
      value: row.hex,
      // Unused ids have no name in tiledata, and an empty line reads as a broken row.
      label: row.name === '' ? row.hex : row.name,
      detail: row.hex,
      imageUrl: row.imageUrl,
    }),
    { param: 'flag', values: TILE_FLAGS },
  ),
  catalogue<Hue>('hues', '/api/v1/admin/hues', (row) => ({
    value: String(row.value),
    label: row.name,
    detail: String(row.value),
    swatch: row.hex,
  })),
  catalogue<Body>('bodies', '/api/v1/admin/bodies', (row) => ({
    value: String(row.body),
    label: row.type,
    detail: row.hex,
    imageUrl: row.imageUrl,
  })),
  catalogue<HairStyle>('hairStyles', '/api/v1/admin/hair-styles', (row) => ({
    value: String(row.style),
    label: row.name,
    detail: row.hex,
    imageUrl: row.imageUrl,
  })),
]
