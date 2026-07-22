import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import '../lib/i18n'
import { ThemeToggle } from './ThemeToggle'

describe('ThemeToggle', () => {
  beforeEach(() => {
    document.documentElement.dataset.theme = 'dark'
    localStorage.clear()
  })

  it('starts from the theme already on the document', () => {
    render(<ThemeToggle />)

    expect(screen.getByRole('button', { pressed: true })).toHaveAccessibleName(/dark/i)
  })

  it('switches the document theme and remembers it', async () => {
    render(<ThemeToggle />)

    await userEvent.click(screen.getByRole('button', { name: /light/i }))

    expect(document.documentElement.dataset.theme).toBe('light')
    expect(localStorage.getItem('mg-theme')).toBe('light')
  })
})
