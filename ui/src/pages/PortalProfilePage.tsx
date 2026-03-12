import { useEffect, useState } from 'react'
import { Button, Chip, Input } from '@heroui/react'
import { useIntl } from 'react-intl'
import { portalApi } from '../api/portalClient'
import { usePortalAuthStore } from '../store/portalAuthStore'

interface PortalCharacter {
  characterId: string
  name: string
  mapId: number
  mapName: string
  x: number
  y: number
}

interface PortalAccount {
  accountId: string
  username: string
  email: string
  accountType: string
  characters: PortalCharacter[]
}

export function PortalProfilePage() {
  const intl = useIntl()
  const user = usePortalAuthStore((s) => s.user)

  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [passwordError, setPasswordError] = useState<string | null>(null)
  const [passwordSuccess, setPasswordSuccess] = useState<string | null>(null)
  const [account, setAccount] = useState<PortalAccount | null>(null)
  const [email, setEmail] = useState('')
  const [savingPassword, setSavingPassword] = useState(false)
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')

  useEffect(() => {
    let mounted = true

    async function load() {
      setLoading(true)
      setError(null)

      try {
        const payload = await portalApi.get<PortalAccount>('/portal/me')
        if (mounted) {
          setAccount(payload)
          setEmail(payload.email ?? '')
        }
      } catch (loadError) {
        if (mounted) {
          setError(loadError instanceof Error ? loadError.message : intl.formatMessage({ id: 'portal.profile.error.load' }))
        }
      } finally {
        if (mounted) {
          setLoading(false)
        }
      }
    }

    void load()

    return () => {
      mounted = false
    }
  }, [intl])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setSuccess(null)

    if (!email.trim()) {
      setError(intl.formatMessage({ id: 'portal.profile.error.emailRequired' }))
      return
    }

    setSaving(true)

    try {
      const updated = await portalApi.put<PortalAccount>('/portal/me', { email: email.trim() })
      setAccount(updated)
      setEmail(updated.email ?? '')
      setSuccess(intl.formatMessage({ id: 'portal.profile.success.saved' }))
    } catch (submitError) {
      setError(submitError instanceof Error ? submitError.message : intl.formatMessage({ id: 'portal.profile.error.save' }))
    } finally {
      setSaving(false)
    }
  }

  async function handlePasswordSubmit(e: React.FormEvent) {
    e.preventDefault()
    setPasswordError(null)
    setPasswordSuccess(null)

    if (!newPassword.trim()) {
      setPasswordError(intl.formatMessage({ id: 'portal.profile.password.error.newRequired' }))
      return
    }

    if (newPassword !== confirmPassword) {
      setPasswordError(intl.formatMessage({ id: 'portal.profile.password.error.confirmMismatch' }))
      return
    }

    if (account?.accountType === 'Regular' && !currentPassword) {
      setPasswordError(intl.formatMessage({ id: 'portal.profile.password.error.currentRequired' }))
      return
    }

    setSavingPassword(true)

    try {
      await portalApi.put<void>('/portal/me/password', {
        currentPassword,
        newPassword,
        confirmPassword,
      })
      setCurrentPassword('')
      setNewPassword('')
      setConfirmPassword('')
      setPasswordSuccess(intl.formatMessage({ id: 'portal.profile.password.success.saved' }))
    } catch (submitError) {
      setPasswordError(
        submitError instanceof Error
          ? submitError.message
          : intl.formatMessage({ id: 'portal.profile.password.error.save' }),
      )
    } finally {
      setSavingPassword(false)
    }
  }

  return (
    <div className="mx-auto flex w-full max-w-[1180px] flex-col gap-6 px-6 py-8 animate-fade-in">
      <div>
        <p className="mb-2 font-mono text-[11px] uppercase tracking-[0.35em]" style={{ color: 'rgba(244,234,215,0.45)' }}>
          {intl.formatMessage({ id: 'portal.profile.label' })}
        </p>
        <h1 className="font-cinzel text-3xl font-semibold" style={{ color: '#f4ead7' }}>
          {intl.formatMessage({ id: 'portal.profile.title' })}
        </h1>
        <p className="mt-3 font-mono text-xs leading-6" style={{ color: 'rgba(244,234,215,0.72)' }}>
          {intl.formatMessage({ id: 'portal.profile.subtitle' })}
        </p>
      </div>

      {(error || success || passwordError || passwordSuccess) && (
        <div className="grid gap-3">
          {error && (
            <div className="rounded-lg border px-4 py-3" style={{ borderColor: 'rgba(211,98,78,0.28)', background: 'rgba(149,44,34,0.16)' }}>
              <p className="font-mono text-xs uppercase tracking-wider" style={{ color: '#f0b0a4' }}>
                {intl.formatMessage({ id: 'portal.account.errorPrefix' })}: {error}
              </p>
            </div>
          )}
          {success && (
            <div className="rounded-lg border px-4 py-3" style={{ borderColor: 'rgba(112,170,117,0.28)', background: 'rgba(43,88,48,0.18)' }}>
              <p className="font-mono text-xs uppercase tracking-wider" style={{ color: '#b9e2bb' }}>
                {success}
              </p>
            </div>
          )}
          {passwordError && (
            <div className="rounded-lg border px-4 py-3" style={{ borderColor: 'rgba(211,98,78,0.28)', background: 'rgba(149,44,34,0.16)' }}>
              <p className="font-mono text-xs uppercase tracking-wider" style={{ color: '#f0b0a4' }}>
                {intl.formatMessage({ id: 'portal.account.errorPrefix' })}: {passwordError}
              </p>
            </div>
          )}
          {passwordSuccess && (
            <div className="rounded-lg border px-4 py-3" style={{ borderColor: 'rgba(112,170,117,0.28)', background: 'rgba(43,88,48,0.18)' }}>
              <p className="font-mono text-xs uppercase tracking-wider" style={{ color: '#b9e2bb' }}>
                {passwordSuccess}
              </p>
            </div>
          )}
        </div>
      )}

      <section
        className="rounded-xl border p-6"
        style={{
          background: 'linear-gradient(180deg, rgba(50,36,24,0.9), rgba(34,25,18,0.92))',
          borderColor: 'rgba(214,179,106,0.16)',
          boxShadow: '0 24px 48px rgba(0,0,0,0.26)',
        }}
      >
        <div className="mb-5 flex items-center justify-between gap-3">
          <h2 className="font-cinzel text-lg font-semibold" style={{ color: '#f9f4ed' }}>
            {intl.formatMessage({ id: 'portal.profile.section.account' })}
          </h2>
          <Chip
            variant="flat"
            className="font-mono text-[11px] uppercase tracking-[0.16em]"
            style={{ background: 'rgba(214,179,106,0.12)', color: '#f4d6a0' }}
          >
            {account?.accountType ?? user?.role ?? intl.formatMessage({ id: 'portal.account.unknown' })}
          </Chip>
        </div>

        <form onSubmit={handleSubmit} className="grid gap-5 md:grid-cols-2">
          <Input
            isReadOnly
            label={intl.formatMessage({ id: 'portal.account.field.username' })}
            value={loading ? '' : (account?.username ?? '')}
            classNames={{
              inputWrapper: 'border-divider',
              label: 'font-mono text-xs tracking-wider',
              input: 'font-mono text-sm text-[#f4ead7]',
            }}
          />

          <Input
            isReadOnly
            label={intl.formatMessage({ id: 'portal.account.field.accountId' })}
            value={loading ? '' : (account?.accountId ?? '')}
            classNames={{
              inputWrapper: 'border-divider',
              label: 'font-mono text-xs tracking-wider',
              input: 'font-mono text-sm text-[#f4ead7]',
            }}
          />

          <Input
            isReadOnly
            label={intl.formatMessage({ id: 'portal.profile.field.accountType' })}
            value={loading ? '' : (account?.accountType ?? '')}
            classNames={{
              inputWrapper: 'border-divider',
              label: 'font-mono text-xs tracking-wider',
              input: 'font-mono text-sm text-[#f4ead7]',
            }}
          />

          <Input
            label={intl.formatMessage({ id: 'portal.account.field.email' })}
            value={email}
            onValueChange={setEmail}
            autoComplete="email"
            isRequired
            classNames={{
              inputWrapper: 'border-divider data-[hover=true]:border-warning',
              label: 'font-mono text-xs tracking-wider',
              input: 'font-mono text-sm text-[#f4ead7]',
            }}
          />

          <div className="md:col-span-2 flex justify-end">
            <Button
              type="submit"
              color="warning"
              isLoading={saving}
              className="font-mono uppercase tracking-[0.18em]"
              style={{
                background: 'linear-gradient(180deg, #d6b36a 0%, #b98b3d 100%)',
                color: '#2a2118',
              }}
            >
              {saving
                ? intl.formatMessage({ id: 'portal.profile.saving' })
                : intl.formatMessage({ id: 'portal.profile.save' })}
            </Button>
          </div>
        </form>
      </section>

      <section
        className="rounded-xl border p-6"
        style={{
          background: 'linear-gradient(180deg, rgba(50,36,24,0.9), rgba(34,25,18,0.92))',
          borderColor: 'rgba(214,179,106,0.16)',
          boxShadow: '0 24px 48px rgba(0,0,0,0.26)',
        }}
      >
        <div className="mb-5 flex items-center justify-between gap-3">
          <h2 className="font-cinzel text-lg font-semibold" style={{ color: '#f9f4ed' }}>
            {intl.formatMessage({ id: 'portal.profile.section.password' })}
          </h2>
          <Chip
            variant="flat"
            className="font-mono text-[11px] uppercase tracking-[0.16em]"
            style={{ background: 'rgba(214,179,106,0.12)', color: '#f4d6a0' }}
          >
            {account?.accountType === 'Regular'
              ? intl.formatMessage({ id: 'portal.profile.password.policy.regular' })
              : intl.formatMessage({ id: 'portal.profile.password.policy.staff' })}
          </Chip>
        </div>

        <form onSubmit={handlePasswordSubmit} className="grid gap-5 md:grid-cols-2">
          {account?.accountType === 'Regular' && (
            <Input
              type="password"
              label={intl.formatMessage({ id: 'portal.profile.password.field.current' })}
              value={currentPassword}
              onValueChange={setCurrentPassword}
              autoComplete="current-password"
              isRequired
              classNames={{
                inputWrapper: 'border-divider data-[hover=true]:border-warning',
                label: 'font-mono text-xs tracking-wider',
                input: 'font-mono text-sm text-[#f4ead7]',
              }}
            />
          )}

          <Input
            type="password"
            label={intl.formatMessage({ id: 'portal.profile.password.field.new' })}
            value={newPassword}
            onValueChange={setNewPassword}
            autoComplete="new-password"
            isRequired
            classNames={{
              inputWrapper: 'border-divider data-[hover=true]:border-warning',
              label: 'font-mono text-xs tracking-wider',
              input: 'font-mono text-sm text-[#f4ead7]',
            }}
          />

          <Input
            type="password"
            label={intl.formatMessage({ id: 'portal.profile.password.field.confirm' })}
            value={confirmPassword}
            onValueChange={setConfirmPassword}
            autoComplete="new-password"
            isRequired
            classNames={{
              inputWrapper: 'border-divider data-[hover=true]:border-warning',
              label: 'font-mono text-xs tracking-wider',
              input: 'font-mono text-sm text-[#f4ead7]',
            }}
          />

          <div className="md:col-span-2 flex justify-end">
            <Button
              type="submit"
              color="warning"
              isLoading={savingPassword}
              className="font-mono uppercase tracking-[0.18em]"
              style={{
                background: 'linear-gradient(180deg, #d6b36a 0%, #b98b3d 100%)',
                color: '#2a2118',
              }}
            >
              {savingPassword
                ? intl.formatMessage({ id: 'portal.profile.password.saving' })
                : intl.formatMessage({ id: 'portal.profile.password.save' })}
            </Button>
          </div>
        </form>
      </section>
    </div>
  )
}
