import { BrowserRouter, Route, Routes } from 'react-router'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { AuthProvider } from './lib/auth'
import { AppShell } from './components/AppShell'
import { RequireAuth } from './routes/RequireAuth'
import { RequireAdmin } from './routes/RequireAdmin'
import { LoginScreen } from './routes/LoginScreen'
import { RegisterScreen } from './routes/RegisterScreen'
import { RegistrationPendingScreen } from './routes/RegistrationPendingScreen'
import { VerifyRegistrationScreen } from './routes/VerifyRegistrationScreen'
import { DashboardScreen } from './routes/DashboardScreen'
import { AdminScreen } from './routes/AdminScreen'
import { MapScreen } from './routes/MapScreen'
import { CharactersScreen } from './routes/CharactersScreen'
import { CharacterDetailScreen } from './routes/CharacterDetailScreen'
import { AdminLayout } from './routes/AdminLayout'
import { AccountsScreen } from './routes/AccountsScreen'
import { CharactersAdminScreen } from './routes/admin/CharactersAdminScreen'
import { ItemTemplatesScreen } from './routes/admin/ItemTemplatesScreen'
import { ItemTemplateDetailScreen } from './routes/admin/ItemTemplateDetailScreen'
import { PluginsScreen } from './routes/PluginsScreen'
import { SettingsScreen } from './routes/SettingsScreen'
import { ConsoleScreen } from './routes/ConsoleScreen'
import { Toaster } from './components/ui/sonner'
import { TooltipProvider } from './components/ui/tooltip'

const queryClient = new QueryClient()

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <TooltipProvider>
        <Toaster />
        <BrowserRouter>
          <AuthProvider>
            <Routes>
              <Route path="/login" element={<LoginScreen />} />
              <Route path="/register" element={<RegisterScreen />} />
              <Route path="/register/pending" element={<RegistrationPendingScreen />} />
              <Route path="/verify" element={<VerifyRegistrationScreen />} />
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
                path="/characters"
                element={
                  <RequireAuth>
                    <AppShell>
                      <CharactersScreen />
                    </AppShell>
                  </RequireAuth>
                }
              />
              <Route
                path="/characters/:serial"
                element={
                  <RequireAuth>
                    <AppShell>
                      <CharacterDetailScreen />
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
                <Route path="characters" element={<CharactersAdminScreen />} />
                <Route path="items" element={<ItemTemplatesScreen />} />
                <Route path="items/:id" element={<ItemTemplateDetailScreen />} />
                <Route path="plugins" element={<PluginsScreen />} />
                <Route path="settings" element={<SettingsScreen />} />
                <Route path="console" element={<ConsoleScreen />} />
              </Route>
            </Routes>
          </AuthProvider>
        </BrowserRouter>
      </TooltipProvider>
    </QueryClientProvider>
  )
}
