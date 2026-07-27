import { render, screen } from '@testing-library/react'
import { StatCard } from './stat-card'

describe('StatCard', () => {
  it('shows the label, value and sub', () => {
    render(<StatCard label="Players online" value={42} sub="+5 today" />)
    expect(screen.getByText('Players online')).toBeInTheDocument()
    expect(screen.getByText('42')).toBeInTheDocument()
    expect(screen.getByText('+5 today')).toBeInTheDocument()
  })

  it('renders an em dash for an undefined value', () => {
    render(<StatCard label="TPS" value={undefined} />)
    expect(screen.getByText('—')).toBeInTheDocument()
  })
})
