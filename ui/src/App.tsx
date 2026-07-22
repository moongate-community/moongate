import { BrowserRouter, Route, Routes } from 'react-router'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { AuthProvider } from './lib/auth'
import { AppShell } from './components/AppShell'
import { RequireAuth } from './routes/RequireAuth'
import { LoginScreen } from './routes/LoginScreen'
import { DashboardScreen } from './routes/DashboardScreen'

const queryClient = new QueryClient()

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
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
          </Routes>
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  )
}
