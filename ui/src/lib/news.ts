import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { apiFetch } from './api'
import type { components } from './api-types'

export type News = components['schemas']['NewsResponse']
export type CreateNews = components['schemas']['CreateNewsRequest']
export type UpdateNews = components['schemas']['UpdateNewsRequest']

const LIST_KEY = ['admin', 'news']

/** Every entry, drafts included, newest first. Staff only. */
export const useNews = () => useQuery({ queryKey: LIST_KEY, queryFn: () => apiFetch<News[]>('/api/v1/admin/news') })

export function useCreateNews() {
  const client = useQueryClient()

  return useMutation({
    mutationFn: (body: CreateNews) =>
      apiFetch<News>('/api/v1/admin/news', { method: 'POST', body: JSON.stringify(body) }),
    onSuccess: () => client.invalidateQueries({ queryKey: LIST_KEY }),
  })
}

export function useUpdateNews() {
  const client = useQueryClient()

  return useMutation({
    mutationFn: ({ id, patch }: { id: number; patch: UpdateNews }) =>
      apiFetch<News>(`/api/v1/admin/news/${id}`, {
        method: 'PUT',
        body: JSON.stringify(patch),
      }),
    onSuccess: () => client.invalidateQueries({ queryKey: LIST_KEY }),
  })
}

export function useDeleteNews() {
  const client = useQueryClient()

  return useMutation({
    mutationFn: (id: number) => apiFetch<void>(`/api/v1/admin/news/${id}`, { method: 'DELETE' }),
    onSuccess: () => client.invalidateQueries({ queryKey: LIST_KEY }),
  })
}
