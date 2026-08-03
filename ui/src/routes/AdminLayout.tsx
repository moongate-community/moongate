import { useState } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router'
import { useTranslation } from 'react-i18next'

import { EntityPicker } from '../components/ui/entity-picker'
import { FilterPill } from '../components/ui/filter-pill'
import { toast } from '../components/ui/sonner'

const linkClass = ({ isActive }: { isActive: boolean }) =>
  isActive
    ? 'flex items-center border-b-2 border-gold py-2 text-sm font-bold text-gold'
    : 'flex items-center border-b-2 border-transparent py-2 text-sm text-muted hover:text-ink'

/**
 * The admin tabs, in groups.
 *
 * The design draws five staff tabs in one row; there are nine, so it does not say what to do here and
 * this is a choice rather than a transcription. They are clustered by what the things are — the shard
 * itself, then its content, then its people, then operating it — and parted by a rule, because nine
 * equal targets in a row is a list you read rather than a bar you aim at.
 *
 * If this ever passes a dozen, the answer is a side rail, not more separators.
 */
const groups: { to: string; key: string; end?: boolean }[][] = [
  [{ to: '/admin', key: 'overview', end: true }],
  [
    { to: '/admin/items', key: 'items' },
    { to: '/admin/mobiles', key: 'mobiles' },
  ],
  [
    { to: '/admin/accounts', key: 'accounts' },
    { to: '/admin/characters', key: 'characters' },
  ],
  [
    { to: '/admin/news', key: 'news' },
    { to: '/admin/plugins', key: 'plugins' },
    { to: '/admin/settings', key: 'settings' },
    { to: '/admin/console', key: 'console' },
  ],
]

/** Which catalogues have a page of their own to open. The rest hand back a value to copy. */
const detailPages: Record<string, string> = {
  itemTemplates: '/admin/items',
  mobileTemplates: '/admin/mobiles',
}

/** The admin area's own tab row, above whichever admin screen is routed below it. */
export function AdminLayout() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [finding, setFinding] = useState(false)

  return (
    <div className="flex flex-col gap-5">
      <nav className="flex flex-wrap items-center gap-x-5 gap-y-1 border-b border-border-subtle">
        {groups.map((group, index) => (
          <div key={group[0].key} className="flex items-center gap-5">
            {index > 0 && <span aria-hidden="true" className="h-4 w-px bg-border-subtle" />}
            {group.map((tab) => (
              <NavLink key={tab.key} to={tab.to} end={tab.end} className={linkClass}>
                {t(`admin.nav.${tab.key}`)}
              </NavLink>
            ))}
          </div>
        ))}

        <FilterPill className="ml-auto mb-1" onClick={() => setFinding(true)}>
          {t('picker.open')}
        </FilterPill>
      </nav>

      <Outlet />

      {/* Mounted only while open: a closed picker would still put a React Query dependency into
          every screen that hosts one, for a dialog nobody is looking at. */}
      {finding && (
        <EntityPicker
          open
          onOpenChange={setFinding}
          onSelect={(entry, catalogue) => {
            const page = detailPages[catalogue.id]

            // A template has somewhere to go. A hue, a body or a raw tile is an id someone is about
            // to type into a YAML file, so the useful thing is to put it on the clipboard.
            if (page) {
              void navigate(`${page}/${encodeURIComponent(entry.value)}`)

              return
            }

            void navigator.clipboard?.writeText(entry.value)
            toast.success(t('picker.copied', { value: entry.value }))
          }}
        />
      )}
    </div>
  )
}
