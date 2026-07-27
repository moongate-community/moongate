import { render, screen } from '@testing-library/react'
import { Alert, AlertDescription, AlertTitle } from './alert'

describe('Alert', () => {
  it('is an alert region carrying the variant class', () => {
    render(
      <Alert variant="danger">
        <AlertTitle>Script error</AlertTitle>
        <AlertDescription>spawn_orc_fort.lua:42</AlertDescription>
      </Alert>,
    )
    const alert = screen.getByRole('alert')
    expect(alert.className).toContain('text-danger-text')
    expect(screen.getByText('Script error')).toBeInTheDocument()
    expect(screen.getByText('spawn_orc_fort.lua:42')).toBeInTheDocument()
  })
})
