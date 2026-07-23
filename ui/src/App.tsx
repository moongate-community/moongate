import { BrowserRouter, Route, Routes } from 'react-router'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { AuthProvider } from './lib/auth'
import { AppShell } from './components/AppShell'
import { RequireAuth } from './routes/RequireAuth'
import { RequireAdmin } from './routes/RequireAdmin'
import { LoginScreen } from './routes/LoginScreen'
import { DashboardScreen } from './routes/DashboardScreen'
import { AdminScreen } from './routes/AdminScreen'
import { AdminLayout } from './routes/AdminLayout'
import { AccountsScreen } from './routes/AccountsScreen'
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
            </Route>
          </Routes>
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  )
}
