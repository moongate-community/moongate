import { render, screen } from '@testing-library/react'
import { Counter, OnlineDot } from './counter'

describe('Counter', () => {
  it('renders the number', () => {
    render(<Counter>8</Counter>)
    expect(screen.getByText('8')).toBeInTheDocument()
  })

  it('OnlineDot has an accessible label', () => {
    render(<OnlineDot />)
    expect(screen.getByRole('img', { name: /online/i })).toBeInTheDocument()
  })
})
