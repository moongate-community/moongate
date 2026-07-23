import * as React from 'react'
import { cn } from '@/lib/utils'

// Faithful to mg-pill: a bordered pill, muted by default; active turns gold with a 10% gold wash.
export function FilterPill({
  active = false,
  className,
  ...props
}: React.ComponentProps<'button'> & { active?: boolean }) {
  return (
    <button
      type="button"
      aria-pressed={active}
      className={cn(
        'rounded-control border px-3 py-1 text-xs',
        active
          ? 'border-gold/35 bg-gold/10 font-bold text-gold'
          : 'border-border-subtle text-muted hover:text-ink',
        className,
      )}
      {...props}
    />
  )
}
