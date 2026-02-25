import { create } from 'zustand'
import { persist } from 'zustand/middleware'

export interface AuthUser {
  accessToken: string
  tokenType: string
  expiresAtUtc: string
  accountId: string
  username: string
  role: string
}

interface AuthState {
  user: AuthUser | null
  login: (user: AuthUser) => void
  logout: () => void
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      login: (user) => set({ user }),
      logout: () => set({ user: null }),
    }),
    { name: 'moongate-auth' },
  ),
)
