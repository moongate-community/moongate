import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Switch } from './switch'

describe('Switch', () => {
  it('toggles and reports the change', async () => {
    const onCheckedChange = vi.fn()
    render(<Switch aria-label="live-tail" onCheckedChange={onCheckedChange} />)
    await userEvent.click(screen.getByRole('switch', { name: 'live-tail' }))
    expect(onCheckedChange).toHaveBeenCalledWith(true)
  })
})
