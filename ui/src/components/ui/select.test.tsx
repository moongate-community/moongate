import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from './select'

describe('Select', () => {
  it('opens and reports the chosen value', async () => {
    const onValueChange = vi.fn()
    render(
      <Select onValueChange={onValueChange}>
        <SelectTrigger aria-label="facet">
          <SelectValue placeholder="Pick" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="felucca">Felucca</SelectItem>
          <SelectItem value="trammel">Trammel</SelectItem>
        </SelectContent>
      </Select>,
    )

    await userEvent.click(screen.getByLabelText('facet'))
    await userEvent.keyboard('{ArrowDown}{ArrowDown}{Enter}')
    expect(onValueChange).toHaveBeenCalledWith('trammel')
  })
})
