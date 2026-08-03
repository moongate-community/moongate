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
    expect(screen.getByRole('link', { name: /characters/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /items/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /mobiles/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /news/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /plugins/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /settings/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /console/i })).toBeInTheDocument()
    expect(screen.getByText('overview page')).toBeInTheDocument()
  })

  it('renders the accounts child at /admin/accounts', () => {
    renderAt('/admin/accounts')
    expect(screen.getByText('accounts page')).toBeInTheDocument()
  })

  // Nine equal targets in a row is a list you read; the rules are what make it a bar you aim at.
  it('parts the tabs into groups', () => {
    const { container } = renderAt('/admin')

    expect(container.querySelectorAll('nav [aria-hidden="true"]')).toHaveLength(3)
  })

  // Related things sit together: the catalogues, then the people, then operating the shard.
  it('keeps related tabs adjacent', () => {
    renderAt('/admin')

    const order = [...document.querySelectorAll('nav a')].map((link) => link.getAttribute('href'))

    expect(order).toEqual([
      '/admin',
      '/admin/items',
      '/admin/mobiles',
      '/admin/accounts',
      '/admin/characters',
      '/admin/news',
      '/admin/plugins',
      '/admin/settings',
      '/admin/console',
    ])
  })
})
