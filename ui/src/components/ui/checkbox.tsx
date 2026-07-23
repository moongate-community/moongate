import * as React from "react"
import { CheckIcon } from "lucide-react"
import { Checkbox as CheckboxPrimitive } from "radix-ui"

import { cn } from "@/lib/utils"

function Checkbox({
  className,
  ...props
}: React.ComponentProps<typeof CheckboxPrimitive.Root>) {
  return (
    <CheckboxPrimitive.Root
      data-slot="checkbox"
      // Faithful: off = border-strong; on = gold with the tick in gold-ink.
      className={cn(
        "peer size-[15px] shrink-0 rounded-control border border-border-strong outline-none focus-visible:border-gold focus-visible:ring-[3px] focus-visible:ring-gold/20 disabled:cursor-not-allowed disabled:opacity-50 data-[state=checked]:border-gold-hi data-[state=checked]:bg-gold data-[state=checked]:text-gold-ink",
        className
      )}
      {...props}
    >
      <CheckboxPrimitive.Indicator
        data-slot="checkbox-indicator"
        className="grid place-content-center text-current transition-none"
      >
        <CheckIcon className="size-3.5" />
      </CheckboxPrimitive.Indicator>
    </CheckboxPrimitive.Root>
  )
}

export { Checkbox }
