import { BrowserRouter, Route, Routes } from 'react-router'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { AuthProvider } from './lib/auth'
import { AppShell } from './components/AppShell'
import { RequireAuth } from './routes/RequireAuth'
import { RequireAdmin } from './routes/RequireAdmin'
import { LoginScreen } from './routes/LoginScreen'
import { DashboardScreen } from './routes/DashboardScreen'
import { AdminScreen } from './routes/AdminScreen'
import { MapScreen } from './routes/MapScreen'
import { AdminLayout } from './routes/AdminLayout'
import { AccountsScreen } from './routes/AccountsScreen'
import { PluginsScreen } from './routes/PluginsScreen'
import { SettingsScreen } from './routes/SettingsScreen'
import { ConsoleScreen } from './routes/ConsoleScreen'
import { Toaster } from './components/ui/sonner'

const queryClient = new QueryClient()

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <Toaster />
      <BrowserRouter>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<LoginScreen />} />
            <Route
              path="/"
              element={
                <RequireAuth>
                  <AppShell>
                    <DashboardScreen />
                  </AppShell>
                </RequireAuth>
              }
            />
            <Route
              path="/map"
              element={
                <RequireAuth>
                  <AppShell>
                    <MapScreen />
                  </AppShell>
                </RequireAuth>
              }
            />
            <Route
              path="/admin"
              element={
                <RequireAuth>
                  <RequireAdmin>
                    <AppShell>
                      <AdminLayout />
                    </AppShell>
                  </RequireAdmin>
                </RequireAuth>
              }
            >
              <Route index element={<AdminScreen />} />
              <Route path="accounts" element={<AccountsScreen />} />
              <Route path="plugins" element={<PluginsScreen />} />
              <Route path="settings" element={<SettingsScreen />} />
              <Route path="console" element={<ConsoleScreen />} />
            </Route>
          </Routes>
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  )
}
