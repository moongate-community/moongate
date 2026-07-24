import * as React from 'react'
import { cva, type VariantProps } from 'class-variance-authority'
import { cn } from '@/lib/utils'

// Faithful to mg-alert: a tinted card, background 8%, border 35%, text in the variant colour.
const alertVariants = cva('rounded-card border p-4 text-sm', {
  variants: {
    variant: {
      danger: 'border-danger/35 bg-danger/[0.08] text-danger-text',
      warning: 'border-gold/35 bg-gold/[0.08] text-gold',
      info: 'border-info/35 bg-info/[0.08] text-info',
    },
  },
  defaultVariants: { variant: 'info' },
})

function Alert({ className, variant, ...props }: React.ComponentProps<'div'> & VariantProps<typeof alertVariants>) {
  return <div role="alert" data-slot="alert" className={cn(alertVariants({ variant }), className)} {...props} />
}

function AlertTitle({ className, ...props }: React.ComponentProps<'div'>) {
  return <div data-slot="alert-title" className={cn('mb-1 font-bold', className)} {...props} />
}

function AlertDescription({ className, ...props }: React.ComponentProps<'div'>) {
  return <div data-slot="alert-description" className={cn('text-muted', className)} {...props} />
}

export { Alert, AlertTitle, AlertDescription }
