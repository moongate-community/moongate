import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch, apiPath } from './api'
import type { components } from './api-types'

export type Account = components['schemas']['AccountResponse']
export type CreateAccount = components['schemas']['CreateAccountRequest']
export type UpdateAccount = components['schemas']['UpdateAccountRequest']

const LIST_KEY = ['admin', 'accounts']
const ACCOUNT = '/api/v1/admin/accounts/{username}'

export const useAccounts = () =>
  useQuery({ queryKey: LIST_KEY, queryFn: () => apiFetch<Account[]>('/api/v1/admin/accounts') })

export function useCreateAccount() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateAccount) =>
      apiFetch<Account>('/api/v1/admin/accounts', { method: 'POST', body: JSON.stringify(body) }),
    onSuccess: () => client.invalidateQueries({ queryKey: LIST_KEY }),
  })
}

export function useUpdateAccount() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ username, patch }: { username: string; patch: UpdateAccount }) =>
      apiFetch<Account>(apiPath(ACCOUNT, { username }), { method: 'PATCH', body: JSON.stringify(patch) }),
    onSuccess: () => client.invalidateQueries({ queryKey: LIST_KEY }),
  })
}

export function useDeleteAccount() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (username: string) => apiFetch<void>(apiPath(ACCOUNT, { username }), { method: 'DELETE' }),
    onSuccess: () => client.invalidateQueries({ queryKey: LIST_KEY }),
  })
}
