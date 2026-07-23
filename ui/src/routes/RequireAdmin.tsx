import { Navigate } from 'react-router'
import type { ReactNode } from 'react'
import { useSession } from '../lib/auth'
import { isAdmin } from '../lib/roles'

/**
 * Role gate for the admin area. A non-admin is sent to the dashboard, not to a dead end — the same
 * principle RequireAuth states: a refused user keeps the shell and its navigation. The anonymous case is
 * already handled by RequireAuth, which wraps this.
 */
export function RequireAdmin({ children }: { children: ReactNode }) {
  const { status, level } = useSession()

  if (status === 'loading') {
    return null
  }

  if (!isAdmin(level)) {
    return <Navigate to="/" replace />
  }

  return <>{children}</>
}
