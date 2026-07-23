import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import '../lib/i18n'
import { AdminLayout } from './AdminLayout'

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/admin" element={<AdminLayout />}>
          <Route index element={<p>overview page</p>} />
          <Route path="accounts" element={<p>accounts page</p>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

describe('AdminLayout', () => {
  it('shows the sub-nav and the index child', () => {
    renderAt('/admin')
    expect(screen.getByRole('link', { name: /overview/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /accounts/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /plugins/i })).toBeInTheDocument()
    expect(screen.getByText('overview page')).toBeInTheDocument()
  })

  it('renders the accounts child at /admin/accounts', () => {
    renderAt('/admin/accounts')
    expect(screen.getByText('accounts page')).toBeInTheDocument()
  })
})
