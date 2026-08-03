import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'

import { Card } from '../../components/ui/card'
import { RarityBadge, rarityColor } from '../../components/templates/RarityBadge'
import { ApiError } from '../../lib/api'
import { useItemTemplate, type ItemTemplate } from '../../lib/templates'

export function ItemTemplateDetailScreen() {
  const { t } = useTranslation()
  const { id = '' } = useParams()
  const template = useItemTemplate(id)

  if (template.isError) {
    // "No such template" and "the request broke" are different answers, and an id typed by hand
    // makes the first one common.
    const key =
      template.error instanceof ApiError && template.error.status === 404
        ? 'admin.templates.notFound'
        : 'admin.templates.loadFailed'

    return (
      <p role="alert" className="text-sm text-danger-text">
        {t(key)}
      </p>
    )
  }

  if (template.data === undefined) {
    return <p className="text-sm text-muted">{t('common.loading')}</p>
  }

  return <Detail template={template.data} />
}

function Detail({ template }: { template: ItemTemplate }) {
  const { t } = useTranslation()
  const name = template.name === '' ? template.id : template.name

  return (
    <section className="space-y-4">
      <Link to="/admin/items" className="text-sm text-muted hover:text-ink">
        {t('admin.templates.back')}
      </Link>

      <Card className="gap-4 p-6">
        <div className="flex items-start gap-4">
          <img src={template.imageUrl} alt={name} className="h-20 w-20 shrink-0 object-contain" />

          <div className="min-w-0">
            <h1 className="text-xl font-bold" style={{ color: rarityColor(template.rarity) }}>
              {name}
            </h1>
            <RarityBadge rarity={template.rarity} />
            {template.description !== '' && <p className="mt-2 text-sm text-muted">{template.description}</p>}
          </div>
        </div>

        <dl className="grid gap-x-8 gap-y-2 text-sm sm:grid-cols-2">
          <Field label={t('admin.templates.id')} value={template.id} />
          <Field label={t('admin.templates.category')} value={template.category} />
          <Field label={t('admin.templates.graphic')} value={`0x${template.itemId.toString(16)}`} />
          <Field label={t('admin.templates.hue')} value={template.hue} />
          <Field label={t('admin.templates.weight')} value={template.weight} />
          <Field label={t('admin.templates.value')} value={template.goldValue} />
          <Field label={t('admin.templates.visibility')} value={template.visibility} />
          <Field label={t('admin.templates.lootType')} value={template.lootType} />
          <Field label={t('admin.templates.movable')} value={yesNo(t, template.isMovable)} />
          <Field label={t('admin.templates.stackable')} value={yesNo(t, template.stackable)} />
          <Field label={t('admin.templates.dyeable')} value={yesNo(t, template.dyeable)} />
          {template.scriptId !== null && template.scriptId !== undefined && (
            <Field label={t('admin.templates.script')} value={template.scriptId} />
          )}
          {template.tags.length > 0 && <Field label={t('admin.templates.tags')} value={template.tags.join(', ')} />}
          {(template.flippableItemIds?.length ?? 0) > 0 && (
            <Field
              label={t('admin.templates.flippable')}
              value={template.flippableItemIds!.map((graphic) => `0x${graphic.toString(16)}`).join(', ')}
            />
          )}
          {(template.lootTables?.length ?? 0) > 0 && (
            <Field label={t('admin.templates.lootTables')} value={template.lootTables!.join(', ')} />
          )}
        </dl>
      </Card>

      {/* Specs are nullable. A card for one the template does not have is noise. */}
      <Spec label={t('admin.templates.equip')} spec={template.equip} />
      <Spec label={t('admin.templates.weapon')} spec={template.weapon} />
      <Spec label={t('admin.templates.container')} spec={template.container} />
      <Spec label={t('admin.templates.book')} spec={template.book} />
      {Object.keys(template.params ?? {}).length > 0 && (
        <Spec label={t('admin.templates.params')} spec={template.params} />
      )}
    </section>
  )
}

/**
 * One spec block, rendered generically from whatever fields it carries. The shapes differ per spec
 * and grow with the game; naming each field here would mean editing this screen every time one
 * gains a property.
 */
function Spec({ label, spec }: { label: string; spec: unknown }) {
  if (spec === null || spec === undefined) {
    return null
  }

  const entries = Object.entries(spec as Record<string, unknown>).filter(
    ([, value]) => value !== null && value !== undefined,
  )

  if (entries.length === 0) {
    return null
  }

  return (
    <Card className="gap-3 p-4">
      <h2 className="font-bold text-ink">{label}</h2>
      <dl className="grid gap-x-8 gap-y-2 text-sm sm:grid-cols-2">
        {entries.map(([key, value]) => (
          <Field key={key} label={key} value={String(value)} />
        ))}
      </dl>
    </Card>
  )
}

function Field({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="flex justify-between gap-4 border-b border-ink/5 pb-1">
      <dt className="text-muted">{label}</dt>
      <dd className="text-right text-ink">{value}</dd>
    </div>
  )
}

/** Several template flags are nullable in the contract; an absent one reads as "no". */
function yesNo(t: (key: string) => string, value: boolean | null | undefined): string {
  return value === true ? t('admin.templates.yes') : t('admin.templates.no')
}
