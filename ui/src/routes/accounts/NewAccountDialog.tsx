// ui/src/routes/accounts/NewAccountDialog.tsx — replaced in full by Task 4
import { Dialog, DialogContent, DialogTitle } from '../../components/ui/dialog'

export function NewAccountDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (o: boolean) => void }) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogTitle>New account</DialogTitle>
      </DialogContent>
    </Dialog>
  )
}
