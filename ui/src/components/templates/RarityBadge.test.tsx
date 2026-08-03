import { render, screen } from '@testing-library/react'

import { RarityBadge } from './RarityBadge'

describe('RarityBadge', () => {
  it('names the rarity', () => {
    render(<RarityBadge rarity="Legendary" />)

    expect(screen.getByText('Legendary')).toBeInTheDocument()
  })

  it('colours each rarity differently', () => {
    const { rerender } = render(<RarityBadge rarity="Rare" />)
    const rare = screen.getByText('Rare').style.color

    rerender(<RarityBadge rarity="Epic" />)

    expect(screen.getByText('Epic').style.color).not.toBe(rare)
  })

  // Artifact is in the enum and was missing from the old portal's colour map — the exact case the
  // fallback exists for, and a reason not to port that map as it was.
  it('colours Artifact', () => {
    render(<RarityBadge rarity="Artifact" />)

    expect(screen.getByText('Artifact').style.color).not.toBe('')
  })

  // Rarity is optional on a template, and an empty badge is worse than no badge.
  it('renders nothing when there is no rarity', () => {
    const { container } = render(<RarityBadge rarity={null} />)

    expect(container).toBeEmptyDOMElement()
  })

  it('renders nothing for a blank rarity', () => {
    const { container } = render(<RarityBadge rarity="" />)

    expect(container).toBeEmptyDOMElement()
  })

  // The enum can gain a member. Naming it beats disappearing.
  it('still names a rarity it has no colour for', () => {
    render(<RarityBadge rarity="Mythical" />)

    expect(screen.getByText('Mythical')).toBeInTheDocument()
  })
})
