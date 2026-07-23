import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import '../lib/i18n'
import { ThemeToggle } from './ThemeToggle'

describe('ThemeToggle', () => {
  beforeEach(() => {
    document.documentElement.dataset.theme = 'dark'
    localStorage.clear()
  })

  it('offers switching to light while dark is active', () => {
    render(<ThemeToggle />)
    expect(screen.getByRole('button', { name: /light/i })).toBeInTheDocument()
  })

  it('switches the document theme and remembers it', async () => {
    render(<ThemeToggle />)

    await userEvent.click(screen.getByRole('button', { name: /light/i }))

    expect(document.documentElement.dataset.theme).toBe('light')
    expect(localStorage.getItem('mg-theme')).toBe('light')

    // The single button now offers the reverse switch.
    expect(screen.getByRole('button', { name: /dark/i })).toBeInTheDocument()
  })
})
