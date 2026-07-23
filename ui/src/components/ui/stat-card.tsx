import { cn } from '@/lib/utils'

// Faithful to the design's statCard: a bordered surface holding an uppercase faint label, a mono value,
// and an optional sub-line. `tone` colours the value (e.g. the gold or danger accents in the mock).
export function StatCard({
  label,
  value,
  sub,
  tone,
  className,
}: {
  label: string
  value: string | number | undefined
  sub?: string
  tone?: string
  className?: string
}) {
  return (
    <div className={cn('rounded-card border border-border-subtle bg-surface px-[18px] py-4', className)}>
      <div className="mb-2 text-[12.5px] uppercase tracking-[0.1em] text-faint">{label}</div>
      <div className={cn('font-mono text-[27px] font-bold text-ink', tone)}>{value ?? '—'}</div>
      {sub !== undefined && <div className="mt-[3px] text-xs text-muted">{sub}</div>}
    </div>
  )
}
