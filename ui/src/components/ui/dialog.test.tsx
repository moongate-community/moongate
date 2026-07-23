import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Dialog, DialogContent, DialogTitle, DialogTrigger } from './dialog'

describe('Dialog', () => {
  it('opens on the trigger and shows the title', async () => {
    render(
      <Dialog>
        <DialogTrigger>Open</DialogTrigger>
        <DialogContent>
          <DialogTitle>Suspend account</DialogTitle>
        </DialogContent>
      </Dialog>,
    )

    expect(screen.queryByText('Suspend account')).not.toBeInTheDocument()
    await userEvent.click(screen.getByText('Open'))
    expect(await screen.findByRole('dialog')).toBeInTheDocument()
    expect(screen.getByText('Suspend account')).toBeInTheDocument()
  })
})
