import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react'
import { ApiError, apiFetch, setAuthToken, setUnauthorizedHandler } from './api'

const STORAGE_KEY = 'mg-token'

/** Renew this long before expiry, so a slow request still lands inside the old token's validity. */
const RENEW_MARGIN_MS = 120_000

type StoredToken = { token: string; expiresAt: string }
type Status = 'loading' | 'authenticated' | 'anonymous'

type Session = {
  status: Status
  username: string | null
  level: string | null
  signIn: (username: string, password: string, remember?: boolean) => Promise<void>
  signOut: () => void
}

const SessionContext = createContext<Session | null>(null)

export function useSession(): Session {
  const session = useContext(SessionContext)
  if (session === null) {
    throw new Error('useSession must be used inside <AuthProvider>')
  }
  return session
}

/**
 * Where "remember me" lives. Ticking it keeps the session in localStorage, so it survives the browser
 * closing; leaving it clear puts the token in sessionStorage, where it dies with the tab. Nothing else
 * in the app needs to know which one holds it — read() checks both, and the writer picks.
 */
function stores(): Storage[] {
  return [localStorage, sessionStorage]
}

/** The stored session and the store holding it, so a renewal can be written back to the same one. */
function read(): { token: StoredToken; store: Storage } | null {
  for (const store of stores()) {
    const raw = store.getItem(STORAGE_KEY)

    if (raw === null) {
      continue
    }

    try {
      const parsed = JSON.parse(raw) as StoredToken

      if (typeof parsed.token === 'string') {
        return { token: parsed, store }
      }
    } catch {
      // Unparseable: treat as absent and let the next store answer.
    }
  }

  return null
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<Status>('loading')
  const [username, setUsername] = useState<string | null>(null)
  const [level, setLevel] = useState<string | null>(null)
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  // Which store the live session is in. Renewals have to land back where sign-in put it, or an
  // unremembered session would silently promote itself to a remembered one on its first renewal.
  const store = useRef<Storage>(localStorage)

  const clear = useCallback(() => {
    clearTimeout(timer.current)

    for (const candidate of stores()) {
      candidate.removeItem(STORAGE_KEY)
    }

    setAuthToken(null)
    setUsername(null)
    setLevel(null)
    setStatus('anonymous')
  }, [])

  // Declared before `adopt` uses it, and read through a ref so `adopt` need not depend on it.
  const renew = useRef<() => Promise<void>>(async () => {})

  const adopt = useCallback((stored: StoredToken) => {
    store.current.setItem(STORAGE_KEY, JSON.stringify(stored))
    setAuthToken(stored.token)

    const delay = new Date(stored.expiresAt).getTime() - Date.now() - RENEW_MARGIN_MS

    clearTimeout(timer.current)
    timer.current = setTimeout(() => {
      void renew.current()
    }, Math.max(delay, 0))
  }, [])

  renew.current = async () => {
    try {
      adopt(await apiFetch<StoredToken>('/api/v1/auth/renew', { method: 'POST' }))
    } catch {
      clear()
    }
  }

  const signIn = useCallback(
    async (user: string, password: string, remember = true) => {
      const issued = await apiFetch<StoredToken>('/api/v1/auth/login', {
        method: 'POST',
        body: JSON.stringify({ username: user, password }),
      })

      store.current = remember ? localStorage : sessionStorage
      adopt(issued)

      const me = await apiFetch<{ username: string; level: string }>('/api/v1/player/me')

      setUsername(me.username)
      setLevel(me.level)
      setStatus('authenticated')
    },
    [adopt],
  )

  useEffect(() => {
    setUnauthorizedHandler(clear)

    const stored = read()

    if (stored === null) {
      setStatus('anonymous')
      return
    }

    // Renewals go back where the session already lives, so reloading an unremembered session does not
    // quietly turn it into a remembered one.
    store.current = stored.store
    adopt(stored.token)

    void (async () => {
      try {
        const me = await apiFetch<{ username: string; level: string }>('/api/v1/player/me')
        setUsername(me.username)
        setLevel(me.level)
        setStatus('authenticated')
      } catch (error) {
        if (!(error instanceof ApiError)) {
          throw error
        }
        clear()
      }
    })()

    return () => clearTimeout(timer.current)
  }, [adopt, clear])

  // Timers in a backgrounded tab are throttled or suspended, so a session left alone can come back past
  // its renewal point. Coming into focus is the cheapest reliable second chance.
  useEffect(() => {
    const onFocus = () => {
      const stored = read()
      if (stored === null) {
        return
      }
      if (new Date(stored.token.expiresAt).getTime() - Date.now() < RENEW_MARGIN_MS) {
        void renew.current()
      }
    }

    window.addEventListener('focus', onFocus)
    return () => window.removeEventListener('focus', onFocus)
  }, [])

  const value = useMemo<Session>(
    () => ({ status, username, level, signIn, signOut: clear }),
    [status, username, level, signIn, clear],
  )

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>
}
