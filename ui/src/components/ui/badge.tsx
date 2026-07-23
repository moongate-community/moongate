import * as React from 'react'
import { cva, type VariantProps } from 'class-variance-authority'
import { cn } from '@/lib/utils'

// Faithful to mg-badge: 700 11px, radius 2px, padding 3px 10px, with the variant tinting text at full
// colour, background at 10% and border at 40%. The staff variant is letter-spaced.
const badgeVariants = cva(
  'inline-flex items-center rounded-control border px-2.5 py-[3px] text-[12px] font-bold leading-none',
  {
    variants: {
      variant: {
        success: 'text-success bg-success/10 border-success/40',
        warning: 'text-gold bg-gold/10 border-gold/40',
        danger: 'text-danger-text bg-danger/10 border-danger/40',
        info: 'text-info bg-info/10 border-info/40',
        staff: 'text-staff bg-staff/10 border-staff/40 tracking-[0.14em]',
      },
    },
    defaultVariants: { variant: 'info' },
  },
)

function Badge({
  className,
  variant,
  ...props
}: React.ComponentProps<'span'> & VariantProps<typeof badgeVariants>) {
  return <span data-slot="badge" className={cn(badgeVariants({ variant }), className)} {...props} />
}

export { Badge, badgeVariants }
