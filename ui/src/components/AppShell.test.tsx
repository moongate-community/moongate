import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import '../lib/i18n'
import { AppShell } from './AppShell'
import { AuthProvider } from '../lib/auth'

function renderShell() {
  return render(
    <MemoryRouter>
      <AuthProvider>
        <AppShell>
          <p>contenuto</p>
        </AppShell>
      </AuthProvider>
    </MemoryRouter>,
  )
}

describe('AppShell', () => {
  beforeEach(() => localStorage.clear())

  it('shows the brand, the tab row and the routed content', () => {
    renderShell()

    expect(screen.getByText('Moongate')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /dashboard/i })).toBeInTheDocument()
    expect(screen.getByText('contenuto')).toBeInTheDocument()
  })

  it('offers the theme toggle', () => {
    renderShell()
    expect(screen.getByRole('button', { name: /scuro/i })).toBeInTheDocument()
  })
})
