import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch, apiPath } from './api'
import type { components } from './api-types'

export type ServerSettings = components['schemas']['ServerSettingsResponse']
export type UpdateSettings = components['schemas']['UpdateServerSettingsRequest']
export type Contacts = components['schemas']['ServerContactsResponse']

const KEY = ['admin', 'server-settings']
const ASSET = '/api/v1/admin/server-settings/assets/{slot}'

export const useServerSettings = () =>
  useQuery({ queryKey: KEY, queryFn: () => apiFetch<ServerSettings>('/api/v1/admin/server-settings') })

export function useUpdateSettings() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: UpdateSettings) =>
      apiFetch<ServerSettings>('/api/v1/admin/server-settings', { method: 'PUT', body: JSON.stringify(body) }),
    onSuccess: () => client.invalidateQueries({ queryKey: KEY }),
  })
}

export function useUploadAsset() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ slot, file }: { slot: string; file: File }) => {
      const form = new FormData()
      form.append('file', file)
      return apiFetch<ServerSettings>(apiPath(ASSET, { slot }), { method: 'POST', body: form })
    },
    onSuccess: () => client.invalidateQueries({ queryKey: KEY }),
  })
}

export function useDeleteAsset() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (slot: string) => apiFetch<void>(apiPath(ASSET, { slot }), { method: 'DELETE' }),
    onSuccess: () => client.invalidateQueries({ queryKey: KEY }),
  })
}
