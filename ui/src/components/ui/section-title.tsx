import type { ReactNode } from 'react'

import { cn } from '@/lib/utils'

/**
 * The design's `cardTitle`: Cinzel, gold, letter-spaced, with room for one action on the right.
 *
 * It exists because twenty-one places were writing a heading by hand, each landing somewhere slightly
 * different — which is how a design system stops being one.
 */
export function SectionTitle({
  children,
  action,
  className,
}: {
  children: ReactNode
  action?: ReactNode
  className?: string
}) {
  return (
    <div className={cn('flex items-baseline justify-between gap-4', className)}>
      <h2 className="font-display text-[13px] font-semibold uppercase tracking-[0.08em] text-gold">{children}</h2>
      {action}
    </div>
  )
}

/**
 * A screen's own heading, above whatever it shows. Larger than a section title and in the ink colour,
 * so the two never compete: one names the page, the other names a block inside it.
 */
export function ScreenTitle({
  children,
  action,
  className,
}: {
  children: ReactNode
  action?: ReactNode
  className?: string
}) {
  return (
    <div className={cn('flex items-end justify-between gap-4', className)}>
      <h1 className="font-display text-xl text-ink">{children}</h1>
      {action}
    </div>
  )
}
