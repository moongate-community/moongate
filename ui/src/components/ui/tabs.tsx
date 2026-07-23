import * as React from 'react'
import { Tabs as TabsPrimitive } from 'radix-ui'

import { cn } from '@/lib/utils'

// Faithful to mg-tabs: a flex row underlined by a border, each tab muted, the active one gold with a
// 2px gold underline. Simplified from the shadcn scaffold's variant/orientation machinery, which the
// design does not use.
function Tabs({ className, ...props }: React.ComponentProps<typeof TabsPrimitive.Root>) {
  return <TabsPrimitive.Root data-slot="tabs" className={cn('flex flex-col gap-4', className)} {...props} />
}

function TabsList({ className, ...props }: React.ComponentProps<typeof TabsPrimitive.List>) {
  return (
    <TabsPrimitive.List
      data-slot="tabs-list"
      className={cn('flex gap-6 border-b border-border-subtle', className)}
      {...props}
    />
  )
}

function TabsTrigger({ className, ...props }: React.ComponentProps<typeof TabsPrimitive.Trigger>) {
  return (
    <TabsPrimitive.Trigger
      data-slot="tabs-trigger"
      className={cn(
        'border-b-2 border-transparent py-3 text-sm text-muted outline-none hover:text-ink disabled:pointer-events-none disabled:opacity-50 data-[state=active]:border-gold data-[state=active]:font-bold data-[state=active]:text-gold',
        className,
      )}
      {...props}
    />
  )
}

function TabsContent({ className, ...props }: React.ComponentProps<typeof TabsPrimitive.Content>) {
  return <TabsPrimitive.Content data-slot="tabs-content" className={cn('outline-none', className)} {...props} />
}

export { Tabs, TabsList, TabsTrigger, TabsContent }
