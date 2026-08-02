import { render, screen } from '@testing-library/react'
import { fireEvent } from '@testing-library/dom'

import { CharacterImage } from './CharacterImage'

describe('CharacterImage', () => {
  it('shows the image', () => {
    render(<CharacterImage src="/a.png" alt="Squid" fallback={<span>none</span>} />)

    expect(screen.getByAltText('Squid')).toHaveAttribute('src', '/a.png')
  })

  // A body with no animation 404s from the image route, and the browser's broken-image icon is not
  // an answer on a page whose point is the picture.
  it('gives way to the fallback when the image fails', () => {
    render(<CharacterImage src="/missing.png" alt="Squid" fallback={<span>none</span>} />)

    fireEvent.error(screen.getByAltText('Squid'))

    expect(screen.queryByAltText('Squid')).not.toBeInTheDocument()
    expect(screen.getByText('none')).toBeInTheDocument()
  })

  // Without this, a row whose image failed keeps its placeholder when the table pages to a
  // different character in the same position.
  it('tries again when the src changes', () => {
    const { rerender } = render(<CharacterImage src="/a.png" alt="One" fallback={<span>none</span>} />)

    fireEvent.error(screen.getByAltText('One'))
    expect(screen.getByText('none')).toBeInTheDocument()

    rerender(<CharacterImage src="/b.png" alt="Two" fallback={<span>none</span>} />)

    expect(screen.getByAltText('Two')).toHaveAttribute('src', '/b.png')
  })
})
