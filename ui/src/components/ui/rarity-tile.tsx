import { useState } from 'react'

import { cn } from '@/lib/utils'

/**
 * Wowhead's rarity palette, which players read without being told. `Common` takes the ink token
 * rather than Wowhead's white, which vanishes on the light theme.
 */
const rarityColors: Record<string, string> = {
  Common: 'var(--color-ink)',
  Uncommon: '#1eff00',
  Rare: '#0070dd',
  Epic: '#a335ee',
  Legendary: '#ff8000',
  Artifact: '#e6cc80',
}

/** The colour for a rarity, or the plain border for something that has none. */
export function rarityColor(rarity: string | null | undefined): string {
  return (rarity && rarityColors[rarity]) || 'var(--color-border-subtle)'
}

/**
 * The design's `tile`: a square carrying an item or character, framed in its rarity and washed with
 * a tenth of that colour.
 *
 * The mock drew an initial inside because it had no art. This shows the real sprite and keeps the
 * initial for when there is none — a body with no animation legitimately 404s, and an empty frame
 * says less than a letter.
 */
export function RarityTile({
  src,
  alt,
  rarity,
  size = 46,
  className,
}: {
  src?: string
  alt: string
  rarity?: string | null
  size?: number
  className?: string
}) {
  const [failed, setFailed] = useState<string | null>(null)
  const colour = rarityColor(rarity)
  const showsArt = src !== undefined && failed !== src

  return (
    <span
      className={cn('inline-flex shrink-0 items-center justify-center rounded-control border', className)}
      style={{
        width: size,
        height: size,
        borderColor: colour,
        background: `color-mix(in srgb, ${colour} 10%, var(--color-deep))`,
      }}
    >
      {showsArt ? (
        <img
          src={src}
          alt={alt}
          onError={() => setFailed(src)}
          className="max-h-full max-w-full object-contain"
          style={{ padding: Math.round(size * 0.08) }}
        />
      ) : (
        <span aria-hidden="true" className="font-display text-[13px] font-bold" style={{ color: colour }}>
          {alt.slice(0, 1).toUpperCase()}
        </span>
      )}
    </span>
  )
}
