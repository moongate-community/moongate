import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from './tooltip'

function renderTooltip() {
  return render(
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger>hover me</TooltipTrigger>
        <TooltipContent>the description</TooltipContent>
      </Tooltip>
    </TooltipProvider>,
  )
}

describe('Tooltip', () => {
  it('says nothing until it is asked to', () => {
    renderTooltip()

    expect(screen.queryByText('the description')).not.toBeInTheDocument()
  })

  // Radix opens on hover and on focus. Focus is what jsdom drives reliably, and it is also the
  // keyboard path a pointer test would never cover.
  it('shows its content when the trigger takes focus', async () => {
    renderTooltip()

    await userEvent.tab()

    expect(await screen.findByText('the description')).toBeInTheDocument()
  })
})
