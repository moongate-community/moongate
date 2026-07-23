import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

/** The small danger-coloured count pill (mg-count). */
export function Counter({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-control bg-danger px-2 py-0.5 text-[11px] font-bold leading-none text-white',
        className,
      )}
    >
      {children}
    </span>
  )
}

/** The 7px success dot used next to online names. */
export function OnlineDot({ className }: { className?: string }) {
  return (
    <span role="img" aria-label="online" className={cn('inline-block size-[7px] rounded-full bg-success', className)} />
  )
}
