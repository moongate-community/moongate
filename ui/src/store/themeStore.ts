import { create } from 'zustand'
import { persist } from 'zustand/middleware'

export type ThemeMode = 'dark' | 'light'

interface ThemeState {
  theme: ThemeMode
  setTheme: (theme: ThemeMode) => void
  toggleTheme: () => void
}

export function applyThemeToDocument(theme: ThemeMode): void {
  const root = document.documentElement
  root.classList.remove('dark', 'light')
  root.classList.add(theme)
}

export const useThemeStore = create<ThemeState>()(
  persist(
    (set, get) => ({
      theme: 'dark',
      setTheme: (theme) => {
        applyThemeToDocument(theme)
        set({ theme })
      },
      toggleTheme: () => {
        const nextTheme = get().theme === 'dark' ? 'light' : 'dark'
        applyThemeToDocument(nextTheme)
        set({ theme: nextTheme })
      },
    }),
    {
      name: 'moongate-theme',
      onRehydrateStorage: () => (state) => {
        applyThemeToDocument(state?.theme ?? 'dark')
      },
    },
  ),
)
