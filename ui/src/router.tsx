import { lazy, Suspense } from 'react'
import type { ReactNode } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { useAuthStore } from './store/authStore'
import { usePortalAuthStore } from './store/portalAuthStore'
import { AppLayout } from './components/AppLayout'
import { LoginPage } from './pages/LoginPage'
import { PortalLoginPage } from './pages/PortalLoginPage'
import { PortalAccountPage } from './pages/PortalAccountPage'
import { PortalProfilePage } from './pages/PortalProfilePage'
import { PortalIntlProvider } from './i18n/PortalIntlProvider'
import { PortalLayout } from './components/PortalLayout'

const DashboardPage = lazy(async () =>
  import('./pages/DashboardPage').then((module) => ({ default: module.DashboardPage })),
)
const UsersPage = lazy(async () =>
  import('./pages/UsersPage').then((module) => ({ default: module.UsersPage })),
)
const AddUserPage = lazy(async () =>
  import('./pages/AddUserPage').then((module) => ({ default: module.AddUserPage })),
)
const ConsolePage = lazy(async () =>
  import('./pages/ConsolePage').then((module) => ({ default: module.ConsolePage })),
)
const ItemTemplatesPage = lazy(async () =>
  import('./pages/ItemTemplatesPage').then((module) => ({ default: module.ItemTemplatesPage })),
)
const ItemTemplateDetailsPage = lazy(async () =>
  import('./pages/ItemTemplateDetailsPage').then((module) => ({ default: module.ItemTemplateDetailsPage })),
)
const ActivePlayersPage = lazy(async () =>
  import('./pages/ActivePlayersPage').then((module) => ({ default: module.ActivePlayersPage })),
)
const MapsPage = lazy(async () =>
  import('./pages/MapsPage').then((module) => ({ default: module.MapsPage })),
)
const HelpTicketsPage = lazy(async () =>
  import('./pages/HelpTicketsPage').then((module) => ({ default: module.HelpTicketsPage })),
)
const HelpTicketDetailsPage = lazy(async () =>
  import('./pages/HelpTicketDetailsPage').then((module) => ({ default: module.HelpTicketDetailsPage })),
)

function ProtectedRoute({ children }: { children: ReactNode }) {
  const user = useAuthStore((s) => s.user)
  return user ? <>{children}</> : <Navigate to="/login" replace />
}

function PortalProtectedRoute({ children }: { children: ReactNode }) {
  const user = usePortalAuthStore((s) => s.user)
  return user ? <>{children}</> : <Navigate to="/portal/login" replace />
}

function RouteFallback() {
  return (
    <div className="flex min-h-[40vh] items-center justify-center">
      <div
        className="rounded-lg border px-4 py-3 font-mono text-xs uppercase tracking-[0.18em]"
        style={{
          color: 'rgba(185,187,211,0.65)',
          borderColor: 'rgba(106,165,218,0.2)',
          background: 'rgba(36,33,48,0.45)',
        }}
      >
        Loading
      </div>
    </div>
  )
}

export function AppRouter() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/portal/login"
        element={(
          <PortalIntlProvider>
            <PortalLoginPage />
          </PortalIntlProvider>
        )}
      />
      <Route
        path="/portal/account"
        element={
          <PortalIntlProvider>
            <PortalProtectedRoute>
              <PortalLayout>
                <PortalAccountPage />
              </PortalLayout>
            </PortalProtectedRoute>
          </PortalIntlProvider>
        }
      />
      <Route
        path="/portal/profile"
        element={
          <PortalIntlProvider>
            <PortalProtectedRoute>
              <PortalLayout>
                <PortalProfilePage />
              </PortalLayout>
            </PortalProtectedRoute>
          </PortalIntlProvider>
        }
      />
      <Route
        path="/"
        element={
          <ProtectedRoute>
            <AppLayout />
          </ProtectedRoute>
        }
      >
        <Route index element={<Navigate to="/dashboard" replace />} />
        <Route
          path="dashboard"
          element={
            <Suspense fallback={<RouteFallback />}>
              <DashboardPage />
            </Suspense>
          }
        />
        <Route
          path="users"
          element={
            <Suspense fallback={<RouteFallback />}>
              <UsersPage />
            </Suspense>
          }
        />
        <Route
          path="users/add"
          element={
            <Suspense fallback={<RouteFallback />}>
              <AddUserPage />
            </Suspense>
          }
        />
        <Route
          path="console"
          element={
            <Suspense fallback={<RouteFallback />}>
              <ConsolePage />
            </Suspense>
          }
        />
        <Route
          path="active-players"
          element={
            <Suspense fallback={<RouteFallback />}>
              <ActivePlayersPage />
            </Suspense>
          }
        />
        <Route
          path="maps"
          element={
            <Suspense fallback={<RouteFallback />}>
              <MapsPage />
            </Suspense>
          }
        />
        <Route
          path="help-tickets"
          element={
            <Suspense fallback={<RouteFallback />}>
              <HelpTicketsPage />
            </Suspense>
          }
        />
        <Route
          path="help-tickets/:ticketId"
          element={
            <Suspense fallback={<RouteFallback />}>
              <HelpTicketDetailsPage />
            </Suspense>
          }
        />
        <Route
          path="item-templates"
          element={
            <Suspense fallback={<RouteFallback />}>
              <ItemTemplatesPage />
            </Suspense>
          }
        />
        <Route
          path="item-templates/:id"
          element={
            <Suspense fallback={<RouteFallback />}>
              <ItemTemplateDetailsPage />
            </Suspense>
          }
        />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
