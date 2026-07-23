import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Card } from '../components/ui/card'
import { Input } from '../components/ui/input'
import { Label } from '../components/ui/label'
import { Button } from '../components/ui/button'
import { Switch } from '../components/ui/switch'
import { toast } from '../components/ui/sonner'
import { useServerSettings, useUpdateSettings, type Contacts } from '../lib/settings'

const EMPTY_CONTACTS: Contacts = { website: null, email: null, discord: null }

export function SettingsScreen() {
  const { t } = useTranslation()
  const settings = useServerSettings()
  const update = useUpdateSettings()

  const [description, setDescription] = useState('')
  const [registration, setRegistration] = useState(false)
  const [contacts, setContacts] = useState<Contacts>(EMPTY_CONTACTS)

  // Seed the form once, the first time the settings arrive. A later background refetch must not
  // clobber edits the operator has already typed but not yet saved.
  const seeded = useRef(false)
  useEffect(() => {
    if (settings.data && !seeded.current) {
      seeded.current = true
      setDescription(settings.data.description ?? '')
      setRegistration(settings.data.registrationEnabled)
      setContacts(settings.data.contacts ?? EMPTY_CONTACTS)
    }
  }, [settings.data])

  async function save(event: FormEvent) {
    event.preventDefault()
    try {
      await update.mutateAsync({
        description: description || null,
        registrationEnabled: registration,
        contacts,
      })
      toast.success(t('admin.settings.saved'))
    } catch {
      toast.error(t('error.generic'))
    }
  }

  const setContact = (key: keyof Contacts) => (value: string) =>
    setContacts((current) => ({ ...current, [key]: value || null }))

  return (
    <form onSubmit={save} className="flex flex-col gap-4">
      <h1 className="font-display text-xl text-ink">{t('admin.settings.title')}</h1>

      <Card className="flex flex-col gap-4 p-5">
        <h2 className="font-display text-[16px] tracking-[0.08em] text-gold">{t('admin.settings.general')}</h2>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="description">{t('admin.settings.description')}</Label>
          <textarea
            id="description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            rows={3}
            className="rounded-control border border-border-subtle bg-deep px-3.5 py-2.5 text-sm text-ink outline-none focus-visible:border-gold"
          />
        </div>
        <Label className="flex items-center gap-2 text-sm">
          <Switch
            checked={registration}
            onCheckedChange={setRegistration}
            aria-label={t('admin.settings.registration')}
          />
          {t('admin.settings.registration')}
        </Label>
      </Card>

      <Card className="flex flex-col gap-4 p-5">
        <h2 className="font-display text-[16px] tracking-[0.08em] text-gold">{t('admin.settings.contacts')}</h2>
        {(['website', 'email', 'discord'] as const).map((key) => (
          <div key={key} className="flex flex-col gap-1.5">
            <Label htmlFor={`contact-${key}`}>{t(`admin.settings.${key}`)}</Label>
            <Input
              id={`contact-${key}`}
              value={contacts[key] ?? ''}
              onChange={(e) => setContact(key)(e.target.value)}
            />
          </div>
        ))}
      </Card>

      <div>
        <Button type="submit" disabled={update.isPending}>
          {t('admin.settings.save')}
        </Button>
      </div>
    </form>
  )
}
