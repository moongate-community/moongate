import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Checkbox } from './checkbox'

describe('Checkbox', () => {
  it('toggles and reports the change', async () => {
    const onCheckedChange = vi.fn()
    render(<Checkbox aria-label="remember" onCheckedChange={onCheckedChange} />)
    await userEvent.click(screen.getByRole('checkbox', { name: 'remember' }))
    expect(onCheckedChange).toHaveBeenCalledWith(true)
  })
})
