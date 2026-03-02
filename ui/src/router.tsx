import type { ReactNode } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { useAuthStore } from './store/authStore'
import { AppLayout } from './components/AppLayout'
import { LoginPage } from './pages/LoginPage'
import { DashboardPage } from './pages/DashboardPage'
import { UsersPage } from './pages/UsersPage'
import { AddUserPage } from './pages/AddUserPage'
import { ConsolePage } from './pages/ConsolePage'
import { ItemTemplatesPage } from './pages/ItemTemplatesPage'
import { ItemTemplateDetailsPage } from './pages/ItemTemplateDetailsPage'
import { ActivePlayersPage } from './pages/ActivePlayersPage'

function ProtectedRoute({ children }: { children: ReactNode }) {
  const user = useAuthStore((s) => s.user)
  return user ? <>{children}</> : <Navigate to="/login" replace />
}

export function AppRouter() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/"
        element={
          <ProtectedRoute>
            <AppLayout />
          </ProtectedRoute>
        }
      >
        <Route index element={<Navigate to="/dashboard" replace />} />
        <Route path="dashboard" element={<DashboardPage />} />
        <Route path="users" element={<UsersPage />} />
        <Route path="users/add" element={<AddUserPage />} />
        <Route path="console" element={<ConsolePage />} />
        <Route path="active-players" element={<ActivePlayersPage />} />
        <Route path="item-templates" element={<ItemTemplatesPage />} />
        <Route path="item-templates/:id" element={<ItemTemplateDetailsPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
