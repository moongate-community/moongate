import { render, screen, waitFor } from '@testing-library/react'
import { Toaster, toast } from './sonner'

describe('Toaster', () => {
  it('shows a toast message', async () => {
    render(<Toaster />)
    toast.success('Account suspended')
    await waitFor(() => expect(screen.getByText('Account suspended')).toBeInTheDocument())
  })
})
