import { Navigate } from 'react-router'
import type { ReactNode } from 'react'
import { useSession } from '../lib/auth'

/**
 * Authentication only — never role checks. A role guard belongs *inside* the shell, so that a refused
 * user still has navigation and a way out instead of a bare dead end.
 */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { status } = useSession()

  if (status === 'loading') {
    return null
  }

  if (status === 'anonymous') {
    return <Navigate to="/login" replace />
  }

  return <>{children}</>
}
