import { useEffect, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useDeleteAccount, useUpdateAccount, type Account, type UpdateAccount } from '../../lib/accounts'
import { Card } from '../../components/ui/card'
import { SectionTitle } from '../../components/ui/section-title'
import { Badge } from '../../components/ui/badge'
import { Input } from '../../components/ui/input'
import { Label } from '../../components/ui/label'
import { Button } from '../../components/ui/button'
import { Switch } from '../../components/ui/switch'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../../components/ui/select'
import { toast } from '../../components/ui/sonner'
import { isAdmin } from '../../lib/roles'

const LEVELS = ['Player', 'GrandMaster', 'Administrator']

/**
 * The detail half of the accounts screen: who the account is, and everything staff can change about
 * it. This is what the design draws beside the table, and it replaces the dialog the portal used to
 * open — a panel keeps the list visible, which is the point of picking a row rather than opening a
 * window over it.
 */
export function AccountDetailPanel({ account }: { account: Account | null }) {
  const { t } = useTranslation()
  const update = useUpdateAccount()
  const remove = useDeleteAccount()

  const [level, setLevel] = useState('Player')
  const [suspended, setSuspended] = useState(false)
  const [password, setPassword] = useState('')
  const [confirmingDelete, setConfirmingDelete] = useState(false)

  // Start over whenever a different row is picked, so no edit leaks from one account to the next.
  useEffect(() => {
    if (account) {
      setLevel(account.level)
      setSuspended(!account.isActive)
      setPassword('')
      setConfirmingDelete(false)
    }
  }, [account])

  if (account === null) {
    return (
      <Card className="flex min-h-40 items-center justify-center p-6 text-center text-sm text-faint">
        {t('admin.accounts.pickOne')}
      </Card>
    )
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
    } catch {
      toast.error(t('error.generic'))
    }
  }

  async function confirmDelete() {
    try {
      await remove.mutateAsync(account!.username)
      toast.success(t('admin.accounts.deleted'))
    } catch {
      toast.error(t('error.generic'))
    }
  }

  return (
    <Card className="flex flex-col gap-4 p-4">
      <div className="flex flex-col gap-2">
        <SectionTitle>{t('admin.accounts.detail')}</SectionTitle>
        <p className="font-display text-lg text-ink">{account.username}</p>
        <p className="font-mono text-xs text-faint">{account.email ?? t('admin.accounts.none')}</p>

        <div className="flex flex-wrap items-center gap-2 pt-1">
          <Badge variant={isAdmin(account.level) ? 'staff' : 'info'}>{account.level}</Badge>
          <Badge variant={account.isActive ? 'success' : 'danger'}>
            {account.isActive ? t('admin.accounts.active') : t('admin.accounts.suspended')}
          </Badge>
          <span className="font-mono text-xs text-muted">
            {t('admin.accounts.characterCount', { count: account.characterCount })}
          </span>
        </div>
      </div>

      <form onSubmit={save} className="flex flex-col gap-4 border-t border-border-subtle pt-4">
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
          <Switch checked={suspended} onCheckedChange={setSuspended} aria-label={t('admin.accounts.suspendedLabel')} />
          {t('admin.accounts.suspendedLabel')}
        </Label>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="account-password">{t('admin.accounts.newPassword')}</Label>
          <Input id="account-password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
          <p className="text-xs text-faint">{t('admin.accounts.newPasswordHint')}</p>
        </div>

        <div className="flex items-center justify-between gap-2 border-t border-border-subtle pt-4">
          {confirmingDelete ? (
            <span className="flex flex-wrap items-center gap-2">
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
        </div>
      </form>
    </Card>
  )
}
