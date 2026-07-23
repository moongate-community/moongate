import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { ApiError } from '../../lib/api'
import { useCreateAccount } from '../../lib/accounts'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '../../components/ui/dialog'
import { Input } from '../../components/ui/input'
import { Label } from '../../components/ui/label'
import { Button } from '../../components/ui/button'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../../components/ui/select'
import { toast } from '../../components/ui/sonner'

const LEVELS = ['Player', 'GrandMaster', 'Administrator']

export function NewAccountDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const { t } = useTranslation()
  const create = useCreateAccount()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [email, setEmail] = useState('')
  const [level, setLevel] = useState('Player')
  const [error, setError] = useState<string | null>(null)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    try {
      await create.mutateAsync({ username, password, email: email || null, level })
      toast.success(t('admin.accounts.created'))
      onOpenChange(false)
    } catch (caught) {
      // 409 is the one failure worth naming inline; anything else is the generic toast.
      if (caught instanceof ApiError && caught.status === 409) {
        setError(t('admin.accounts.taken'))
      } else {
        toast.error(t('error.generic'))
      }
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <form onSubmit={submit} className="flex flex-col gap-4">
          <DialogHeader>
            <DialogTitle>{t('admin.accounts.new')}</DialogTitle>
          </DialogHeader>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="new-username">{t('admin.accounts.username')}</Label>
            <Input id="new-username" value={username} onChange={(e) => setUsername(e.target.value)} required />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="new-password">{t('admin.accounts.password')}</Label>
            <Input
              id="new-password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="new-email">{t('admin.accounts.email')}</Label>
            <Input id="new-email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
          </div>
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

          {error !== null && (
            <p role="alert" className="text-sm text-danger-text">
              {error}
            </p>
          )}

          <DialogFooter>
            <Button type="submit" disabled={create.isPending}>
              {t('admin.accounts.create')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
