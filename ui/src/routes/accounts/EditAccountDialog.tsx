// ui/src/routes/accounts/EditAccountDialog.tsx — replaced in full by Task 5
import type { Account } from '../../lib/accounts'

export function EditAccountDialog({ account }: { account: Account | null; onOpenChange: (o: boolean) => void }) {
  return account ? null : null
}
