/**
 * Wowhead's rarity palette, which players already read without being told. `Common` is the
 * exception: Wowhead paints it white, which vanishes on this portal's light theme, so it takes the
 * ink token and follows the theme instead.
 *
 * `Artifact` is in the server's enum and was missing from the old portal's map — which is why the
 * lookup falls back rather than assuming the set is closed.
 */
const rarityColors: Record<string, string> = {
  Common: 'var(--color-ink)',
  Uncommon: '#1eff00',
  Rare: '#0070dd',
  Epic: '#a335ee',
  Legendary: '#ff8000',
  Artifact: '#e6cc80',
}

/** The colour for a rarity, or the muted token for one this portal has no word for. */
export function rarityColor(rarity: string | null | undefined): string {
  return (rarity && rarityColors[rarity]) || 'var(--color-muted)'
}

/** The rarity in its own colour, or nothing at all when the template has none. */
export function RarityBadge({ rarity }: { rarity: string | null | undefined }) {
  if (rarity === null || rarity === undefined || rarity === '') {
    return null
  }

  return (
    <span className="text-xs font-bold" style={{ color: rarityColor(rarity) }}>
      {rarity}
    </span>
  )
}
