import { Toaster as Sonner, toast } from 'sonner'

// A single Toaster mounted at the app root. Styled onto the surface tokens; sonner keeps the portal,
// stacking and duration bar. Variants are reached through toast.success/error/warning/info.
export function Toaster() {
  return (
    <Sonner
      position="bottom-right"
      toastOptions={{
        classNames: {
          toast: 'rounded-card border border-border-subtle bg-surface text-ink shadow-lg',
          description: 'text-muted',
          success: 'border-success/40',
          error: 'border-danger/40',
          warning: 'border-gold/40',
          info: 'border-info/40',
        },
      }}
    />
  )
}

export { toast }
