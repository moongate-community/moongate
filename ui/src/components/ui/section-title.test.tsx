import { render, screen } from '@testing-library/react'

import { ScreenTitle, SectionTitle } from './section-title'

describe('SectionTitle', () => {
  it('names the block', () => {
    render(<SectionTitle>Equipment</SectionTitle>)

    expect(screen.getByRole('heading', { name: 'Equipment' })).toBeInTheDocument()
  })

  it('carries an action beside it', () => {
    render(<SectionTitle action={<button type="button">Manage</button>}>Equipment</SectionTitle>)

    expect(screen.getByRole('button', { name: 'Manage' })).toBeInTheDocument()
  })

  // The two titles are different ranks, not two sizes of the same thing: one names the page and the
  // other a block inside it, so a screen reader hears the hierarchy the design draws.
  it('sits below a screen title in the outline', () => {
    render(
      <>
        <ScreenTitle>Item Templates</ScreenTitle>
        <SectionTitle>Equipment</SectionTitle>
      </>,
    )

    expect(screen.getByRole('heading', { level: 1, name: 'Item Templates' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 2, name: 'Equipment' })).toBeInTheDocument()
  })
})
