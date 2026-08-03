import { render, screen } from '@testing-library/react'
import { fireEvent } from '@testing-library/dom'

import { RarityTile, rarityColor } from './rarity-tile'

describe('RarityTile', () => {
  it('shows the art it was given', () => {
    render(<RarityTile src="/sword.png" alt="Katana" rarity="Rare" />)

    expect(screen.getByAltText('Katana')).toHaveAttribute('src', '/sword.png')
  })

  // The frame is the point: it carries the rarity the sprite itself cannot.
  it('frames the art in its rarity', () => {
    const { container } = render(<RarityTile src="/sword.png" alt="Katana" rarity="Epic" />)

    // jsdom normalises the hex to rgb(), so this compares against what the browser would compute.
    expect((container.firstChild as HTMLElement).style.borderColor).toBe('rgb(163, 53, 238)')
  })

  it('falls back to a plain frame for something with no rarity', () => {
    const { container } = render(<RarityTile src="/orc.png" alt="an orc" />)

    expect((container.firstChild as HTMLElement).style.borderColor).toBe('var(--color-border-subtle)')
  })

  // A body with no animation legitimately 404s, and an empty frame says less than a letter.
  it('shows an initial when the art fails', () => {
    render(<RarityTile src="/missing.png" alt="Katana" rarity="Rare" />)

    fireEvent.error(screen.getByAltText('Katana'))

    expect(screen.queryByAltText('Katana')).not.toBeInTheDocument()
    expect(screen.getByText('K')).toBeInTheDocument()
  })

  it('shows an initial when there is no art at all', () => {
    render(<RarityTile alt="Katana" rarity="Rare" />)

    expect(screen.getByText('K')).toBeInTheDocument()
  })

  // Artifact is in the server's enum and was missing from the old portal's map.
  it('colours Artifact', () => {
    expect(rarityColor('Artifact')).not.toBe(rarityColor(null))
  })
})
