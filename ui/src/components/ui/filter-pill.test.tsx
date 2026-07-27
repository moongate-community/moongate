import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { FilterPill } from './filter-pill'

describe('FilterPill', () => {
  it('reflects the active state through aria-pressed', () => {
    render(<FilterPill active>Open · 8</FilterPill>)
    expect(screen.getByRole('button', { pressed: true })).toHaveTextContent('Open · 8')
  })

  it('fires onClick', async () => {
    const onClick = vi.fn()
    render(<FilterPill onClick={onClick}>Mine · 3</FilterPill>)
    await userEvent.click(screen.getByRole('button'))
    expect(onClick).toHaveBeenCalledOnce()
  })
})
