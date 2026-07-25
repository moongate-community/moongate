import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Card } from '../components/ui/card'
import { Input } from '../components/ui/input'
import { Label } from '../components/ui/label'
import { Button } from '../components/ui/button'
import { Switch } from '../components/ui/switch'
import { toast } from '../components/ui/sonner'
import { AssetSlotRow } from '../components/settings/AssetSlotRow'
import { useServerSettings, useUpdateSettings, type Contacts } from '../lib/settings'

const EMPTY_CONTACTS: Contacts = { website: null, email: null, discord: null }
const ASSET_SLOTS = ['logo', 'favicon', 'banner'] as const

function isValidWebsite(website: string | null | undefined): boolean {
  if (website === null || website === undefined || !/^https?:\/\/[^/]/i.test(website)) {
    return false
  }

  try {
    const url = new URL(website)
    return (url.protocol === 'http:' || url.protocol === 'https:') && url.host !== ''
  } catch {
    return false
  }
}

export function SettingsScreen() {
  const { t } = useTranslation()
  const settings = useServerSettings()
  const update = useUpdateSettings()

  const [description, setDescription] = useState('')
  const [tagline, setTagline] = useState('')
  const [registration, setRegistration] = useState(false)
  const [contacts, setContacts] = useState<Contacts>(EMPTY_CONTACTS)

  // Seed the form once, the first time the settings arrive. A later background refetch must not
  // clobber edits the operator has already typed but not yet saved.
  const seeded = useRef(false)
  useEffect(() => {
    if (settings.data && !seeded.current) {
      seeded.current = true
      setDescription(settings.data.description ?? '')
      setTagline(settings.data.tagline ?? '')
      setRegistration(settings.data.registrationEnabled)
      setContacts(settings.data.contacts ?? EMPTY_CONTACTS)
    }
  }, [settings.data])

  async function save(event: FormEvent) {
    event.preventDefault()
    try {
      await update.mutateAsync({
        description: description || null,
        tagline: tagline || null,
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

  const websiteValid = isValidWebsite(contacts.website)
  const emailChannelSelected = settings.data?.registrationReadiness.emailChannelSelected ?? false
  const emailChannelAvailable = settings.data?.registrationReadiness.emailChannelAvailable ?? false
  const registrationReady = websiteValid && emailChannelSelected && emailChannelAvailable

  // The API keys the assets map in PascalCase ("Logo"), while the slot (and its upload path) is
  // lowercase — match case-insensitively so the preview finds the stored image.
  const assetUrl = (slot: string) =>
    Object.entries(settings.data?.assets ?? {}).find(([key]) => key.toLowerCase() === slot)?.[1]

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
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="tagline">{t('admin.settings.tagline')}</Label>
          <Input id="tagline" value={tagline} onChange={(e) => setTagline(e.target.value)} />
        </div>
        <Label className="flex items-center gap-2 text-sm">
          <Switch
            checked={registration}
            onCheckedChange={setRegistration}
            disabled={!registration && !registrationReady}
            aria-label={t('admin.settings.registration')}
          />
          {t('admin.settings.registration')}
        </Label>

        <section aria-labelledby="registration-readiness-title" className="flex flex-col gap-3">
          <h3 id="registration-readiness-title" className="font-display text-sm tracking-wide text-ink">
            {t('admin.settings.readiness.title')}
          </h3>
          <dl className="grid gap-2 text-sm">
            <ReadinessRow
              label={t('admin.settings.readiness.overall')}
              value={registrationReady}
              positive={t('admin.settings.readiness.ready')}
              negative={t('admin.settings.readiness.notReady')}
            />
            <ReadinessRow
              label={t('admin.settings.readiness.website')}
              value={websiteValid}
              positive={t('admin.settings.readiness.valid')}
              negative={t('admin.settings.readiness.invalid')}
            />
            <ReadinessRow
              label={t('admin.settings.readiness.emailSelected')}
              value={emailChannelSelected}
              positive={t('admin.settings.readiness.selected')}
              negative={t('admin.settings.readiness.notSelected')}
            />
            <ReadinessRow
              label={t('admin.settings.readiness.emailAvailable')}
              value={emailChannelAvailable}
              positive={t('admin.settings.readiness.available')}
              negative={t('admin.settings.readiness.unavailable')}
            />
          </dl>
          <p className="text-xs leading-relaxed text-faint">{t('admin.settings.readiness.guidance')}</p>
        </section>
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

      <Card className="flex flex-col gap-4 p-5">
        <h2 className="font-display text-[16px] tracking-[0.08em] text-gold">{t('admin.settings.assets')}</h2>
        {ASSET_SLOTS.map((slot) => (
          <AssetSlotRow key={slot} slot={slot} label={t(`admin.settings.${slot}`)} url={assetUrl(slot)} />
        ))}
      </Card>
    </form>
  )
}

function ReadinessRow({
  label,
  value,
  positive,
  negative,
}: {
  label: string
  value: boolean
  positive: string
  negative: string
}) {
  return (
    <div className="flex items-center justify-between gap-4">
      <dt className="text-muted">{label}</dt>
      <dd className={value ? 'text-success' : 'text-danger-text'}>{value ? positive : negative}</dd>
    </div>
  )
}
