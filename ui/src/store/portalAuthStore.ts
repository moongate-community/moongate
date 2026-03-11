import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { AuthUser } from './authStore'

interface PortalAuthState {
  user: AuthUser | null
  login: (user: AuthUser) => void
  logout: () => void
}

export const usePortalAuthStore = create<PortalAuthState>()(
  persist(
    (set) => ({
      user: null,
      login: (user) => set({ user }),
      logout: () => set({ user: null }),
    }),
    { name: 'moongate-portal-auth' },
  ),
)
