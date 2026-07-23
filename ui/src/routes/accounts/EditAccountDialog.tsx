import { useEffect, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useDeleteAccount, useUpdateAccount, type Account, type UpdateAccount } from '../../lib/accounts'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '../../components/ui/dialog'
import { Input } from '../../components/ui/input'
import { Label } from '../../components/ui/label'
import { Button } from '../../components/ui/button'
import { Switch } from '../../components/ui/switch'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../../components/ui/select'
import { toast } from '../../components/ui/sonner'

const LEVELS = ['Player', 'GrandMaster', 'Administrator']

export function EditAccountDialog({
  account,
  onOpenChange,
}: {
  account: Account | null
  onOpenChange: (open: boolean) => void
}) {
  const { t } = useTranslation()
  const update = useUpdateAccount()
  const remove = useDeleteAccount()

  const [level, setLevel] = useState('Player')
  const [suspended, setSuspended] = useState(false)
  const [password, setPassword] = useState('')
  const [confirmingDelete, setConfirmingDelete] = useState(false)

  // Reset the form whenever a different account opens the dialog.
  useEffect(() => {
    if (account) {
      setLevel(account.level)
      setSuspended(!account.isActive)
      setPassword('')
      setConfirmingDelete(false)
    }
  }, [account])

  if (account === null) {
    return null
  }

  async function save(event: FormEvent) {
    event.preventDefault()
    // Only the fields that actually changed, so a save never rewrites what it did not touch.
    const patch: UpdateAccount = {}
    if (level !== account!.level) patch.level = level
    if (suspended === account!.isActive) patch.isActive = !suspended
    if (password.length > 0) patch.password = password

    try {
      if (Object.keys(patch).length > 0) {
        await update.mutateAsync({ username: account!.username, patch })
      }
      toast.success(t('admin.accounts.saved'))
      onOpenChange(false)
    } catch {
      toast.error(t('error.generic'))
    }
  }

  async function confirmDelete() {
    try {
      await remove.mutateAsync(account!.username)
      toast.success(t('admin.accounts.deleted'))
      onOpenChange(false)
    } catch {
      toast.error(t('error.generic'))
    }
  }

  return (
    <Dialog open={account !== null} onOpenChange={onOpenChange}>
      <DialogContent>
        <form onSubmit={save} className="flex flex-col gap-4">
          <DialogHeader>
            <DialogTitle>{t('admin.accounts.edit', { username: account.username })}</DialogTitle>
          </DialogHeader>

          <div className="flex flex-col gap-1.5">
            <Label>{t('admin.accounts.level')}</Label>
            <Select value={level} onValueChange={setLevel}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {LEVELS.map((l) => (
                  <SelectItem key={l} value={l}>
                    {t(`admin.accounts.levels.${l}`)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <Label className="flex items-center gap-2 text-sm">
            <Switch
              checked={suspended}
              onCheckedChange={setSuspended}
              aria-label={t('admin.accounts.suspendedLabel')}
            />
            {t('admin.accounts.suspendedLabel')}
          </Label>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="edit-password">{t('admin.accounts.newPassword')}</Label>
            <Input id="edit-password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
            <p className="text-xs text-faint">{t('admin.accounts.newPasswordHint')}</p>
          </div>

          <DialogFooter className="flex items-center justify-between gap-2">
            {confirmingDelete ? (
              <span className="flex items-center gap-2">
                <span className="text-sm text-danger-text">
                  {t('admin.accounts.deleteConfirm', { username: account.username })}
                </span>
                <Button type="button" variant="destructive" onClick={confirmDelete} disabled={remove.isPending}>
                  {t('admin.accounts.deleteYes')}
                </Button>
              </span>
            ) : (
              <Button
                type="button"
                variant="ghost"
                className="text-danger-text"
                onClick={() => setConfirmingDelete(true)}
              >
                {t('admin.accounts.delete')}
              </Button>
            )}
            <Button type="submit" disabled={update.isPending}>
              {t('admin.accounts.save')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
