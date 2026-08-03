import { useQuery } from '@tanstack/react-query'

import { apiFetch } from './api'
import type { components } from './api-types'
import { CATALOGUE_PAGE_SIZE, cataloguePath } from './catalogues'

export type ItemTemplateSummary = components['schemas']['ItemTemplateSummaryResponse']
export type ItemTemplate = components['schemas']['ItemTemplateResponse']
export type ItemTemplatePage = components['schemas']['ItemTemplateSummaryResponsePagedResponse']

/** The server's own default. Named here so the pager and the request cannot disagree. */
export const TEMPLATES_PAGE_SIZE = CATALOGUE_PAGE_SIZE

/**
 * The catalogue URL. A blank search is omitted rather than sent empty: the server reads a blank
 * `search` as "no filter" anyway, and leaving it out keeps the query key stable so paging with an
 * empty box does not miss the cache.
 */
export function itemTemplatesQuery(params: { page: number; search: string }): string {
  return cataloguePath('/api/v1/admin/items/templates', params)
}

/** Every item template, paged and searched by the server. Staff only. */
export const useItemTemplates = (params: { page: number; search: string }) =>
  useQuery({
    queryKey: ['admin', 'itemTemplates', params.page, params.search.trim()],
    queryFn: () => apiFetch<ItemTemplatePage>(itemTemplatesQuery(params)),
    // Holding the previous page while the next loads is what makes it read as a table rather than a
    // slideshow.
    placeholderData: (previous: ItemTemplatePage | undefined) => previous,
  })

/**
 * One item template in full, specs included. The id is escaped because template ids come from YAML
 * and are not guaranteed to be URL-safe.
 */
export const useItemTemplate = (id: string) =>
  useQuery({
    queryKey: ['admin', 'itemTemplates', id],
    queryFn: () => apiFetch<ItemTemplate>(`/api/v1/admin/items/templates/${encodeURIComponent(id)}`),
  })

export type MobileTemplateSummary = components['schemas']['MobileTemplateSummaryResponse']
export type MobileTemplate = components['schemas']['MobileTemplateResponse']
export type MobileTemplatePage = components['schemas']['MobileTemplateSummaryResponsePagedResponse']

/** The catalogue URL for spawn templates. Blank searches are omitted, as in {@link itemTemplatesQuery}. */
export function mobileTemplatesQuery(params: { page: number; search: string }): string {
  return cataloguePath('/api/v1/admin/mobiles/templates', params)
}

/** Every mobile spawn template, paged and searched by the server. Staff only. */
export const useMobileTemplates = (params: { page: number; search: string }) =>
  useQuery({
    queryKey: ['admin', 'mobileTemplates', params.page, params.search.trim()],
    queryFn: () => apiFetch<MobileTemplatePage>(mobileTemplatesQuery(params)),
    placeholderData: (previous: MobileTemplatePage | undefined) => previous,
  })

/** One mobile template in full, variants and equipment included. */
export const useMobileTemplate = (id: string) =>
  useQuery({
    queryKey: ['admin', 'mobileTemplates', id],
    queryFn: () => apiFetch<MobileTemplate>(`/api/v1/admin/mobiles/templates/${encodeURIComponent(id)}`),
  })
