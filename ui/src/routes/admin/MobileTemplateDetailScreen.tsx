import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'

import { Card } from '../../components/ui/card'
import { SectionTitle } from '../../components/ui/section-title'
import { ApiError } from '../../lib/api'
import { useMobileTemplate, type MobileTemplate } from '../../lib/templates'

type Appearance = MobileTemplate['appearance']
type Equipment = MobileTemplate['equipment']
type Variant = MobileTemplate['variants'][number]

export function MobileTemplateDetailScreen() {
  const { t } = useTranslation()
  const { id = '' } = useParams()
  const template = useMobileTemplate(id)

  if (template.isError) {
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

function Detail({ template }: { template: MobileTemplate }) {
  const { t } = useTranslation()
  const name = template.name === '' ? template.id : template.name

  return (
    <section className="space-y-4">
      <Link to="/admin/mobiles" className="text-sm text-muted hover:text-ink">
        {t('admin.templates.back')}
      </Link>

      <Card className="gap-4 p-6">
        <div className="flex items-start gap-4">
          <img src={template.imageUrl} alt={name} className="h-24 w-20 shrink-0 object-contain" />
          <img src={template.paperdollUrl} alt={name} className="h-40 w-32 shrink-0 object-contain" />

          <div className="min-w-0">
            <h1 className="text-xl font-bold text-ink">{name}</h1>
            {template.title !== '' && <p className="text-sm text-muted">{template.title}</p>}
            {template.description !== '' && <p className="mt-2 text-sm text-muted">{template.description}</p>}
          </div>
        </div>

        <dl className="grid gap-x-8 gap-y-2 text-sm sm:grid-cols-2">
          <Field label={t('admin.templates.id')} value={template.id} />
          <Field label={t('admin.templates.category')} value={template.category} />
          {/* Null means the template lets a spawn pick, which is not the same as male. */}
          <Field label={t('admin.templates.gender')} value={template.gender ?? t('admin.templates.anyGender')} />
          {template.namePool !== '' && <Field label={t('admin.templates.namePool')} value={template.namePool} />}
          {template.baseMobile && <Field label={t('admin.templates.base')} value={template.baseMobile} />}
          <Field
            label={t('admin.templates.stats')}
            value={`${template.strength} / ${template.dexterity} / ${template.intelligence}`}
          />
          {template.lootTableId && <Field label={t('admin.templates.lootTable')} value={template.lootTableId} />}
          {template.brainScript && <Field label={t('admin.templates.brain')} value={template.brainScript} />}
          {template.tags.length > 0 && <Field label={t('admin.templates.tags')} value={template.tags.join(', ')} />}
        </dl>
      </Card>

      <Card className="gap-3 p-4">
        <SectionTitle>{t('admin.templates.appearance')}</SectionTitle>
        <AppearanceFields appearance={template.appearance} />
      </Card>

      {Object.keys(template.skills).length > 0 && (
        <Card className="gap-3 p-4">
          <SectionTitle>{t('admin.templates.skills')}</SectionTitle>
          <dl className="grid gap-x-8 gap-y-2 text-sm sm:grid-cols-2">
            {Object.entries(template.skills).map(([skill, value]) => (
              <Field key={skill} label={skill} value={value} />
            ))}
          </dl>
        </Card>
      )}

      {template.equipment.length > 0 && (
        <Card className="gap-3 p-4">
          <SectionTitle>{t('admin.templates.equipment')}</SectionTitle>
          <EquipmentList equipment={template.equipment} />
        </Card>
      )}

      {template.variants.map((variant) => (
        <VariantCard key={variant.name} variant={variant} />
      ))}
    </section>
  )
}

function VariantCard({ variant }: { variant: Variant }) {
  const { t } = useTranslation()

  return (
    <Card className="gap-3 p-4">
      <SectionTitle
        action={
          <span className="font-mono text-xs text-muted">
            {t('admin.templates.weight')} {variant.weight}
          </span>
        }
      >
        {variant.name}
      </SectionTitle>

      <dl className="grid gap-x-8 gap-y-2 text-sm sm:grid-cols-2">
        <Field label={t('admin.templates.gender')} value={variant.gender ?? t('admin.templates.anyGender')} />
        {variant.namePool && <Field label={t('admin.templates.namePool')} value={variant.namePool} />}
        {variant.lootTableId && <Field label={t('admin.templates.lootTable')} value={variant.lootTableId} />}
      </dl>

      <AppearanceFields appearance={variant.appearance} />
      {variant.equipment.length > 0 && <EquipmentList equipment={variant.equipment} />}
    </Card>
  )
}

/**
 * The hues are specs, not numbers — `hue(1002:1058)` is a range resolved once per spawn — so they
 * are shown verbatim rather than parsed into something that would lose the range.
 */
function AppearanceFields({ appearance }: { appearance: Appearance }) {
  const { t } = useTranslation()

  return (
    <dl className="grid gap-x-8 gap-y-2 text-sm sm:grid-cols-2">
      <Field label={t('admin.templates.body')} value={appearance.body} />
      {appearance.skinHue && <Field label="Skin hue" value={appearance.skinHue} />}
      {appearance.hairStyle > 0 && <Field label="Hair style" value={appearance.hairStyle} />}
      {appearance.hairHue && <Field label="Hair hue" value={appearance.hairHue} />}
      {appearance.facialHairStyle > 0 && <Field label="Facial hair" value={appearance.facialHairStyle} />}
      {appearance.facialHairHue && <Field label="Facial hair hue" value={appearance.facialHairHue} />}
    </dl>
  )
}

function EquipmentList({ equipment }: { equipment: Equipment }) {
  return (
    <ul className="flex flex-col text-sm">
      {equipment.map((worn) => (
        <li key={`${worn.layer}-${worn.item}`} className="flex justify-between gap-4 border-b border-ink/5 py-1">
          <span className="text-muted">{worn.layer}</span>
          <span className="text-ink">
            {worn.item}
            {worn.hue && <span className="ml-2 text-xs text-muted">{worn.hue}</span>}
          </span>
        </li>
      ))}
    </ul>
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
