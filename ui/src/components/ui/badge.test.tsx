import { render, screen } from '@testing-library/react'
import { Badge } from './badge'

describe('Badge', () => {
  it('renders its content', () => {
    render(<Badge variant="success">ACTIVE</Badge>)
    expect(screen.getByText('ACTIVE')).toBeInTheDocument()
  })

  it('carries the variant colour class', () => {
    render(<Badge variant="danger">BANNED</Badge>)
    expect(screen.getByText('BANNED').className).toContain('text-danger-text')
  })

  it('letter-spaces the staff variant', () => {
    render(<Badge variant="staff">GM</Badge>)
    expect(screen.getByText('GM').className).toContain('tracking-[0.14em]')
  })
})
