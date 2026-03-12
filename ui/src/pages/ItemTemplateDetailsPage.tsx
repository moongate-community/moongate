import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { Button, Chip, Spinner } from '@heroui/react'
import { api } from '../api/client'
import { ItemTemplatePreview } from '../components/ItemTemplatePreview'

interface ItemTemplateContainerItem {
  id: string
  name: string
  itemId: string
}

interface ItemTemplateDetail {
  id: string
  name: string
  category: string
  itemId: string
  description?: string
  tags?: string[]
  scriptId?: string
  rarity?: string
  weight?: number
  goldValue?: string
  hue?: string
  gumpId?: string
  container?: string[]
  containerItems?: ItemTemplateContainerItem[]
  params?: Record<string, { type: number; value: string }>
}

function itemTemplateParamTypeToLabel(type: number): string {
  switch (type) {
    case 0:
      return 'string'
    case 1:
      return 'serial'
    case 2:
      return 'hue'
    default:
      return `unknown(${type})`
  }
}

function parseHueNumber(hue?: string): number | null {
  if (!hue) {
    return null
  }

  const normalizedHue = hue.trim()
  if (normalizedHue.length === 0) {
    return null
  }

  if (normalizedHue.startsWith('0x') || normalizedHue.startsWith('0X')) {
    const parsedHex = Number.parseInt(normalizedHue.slice(2), 16)
    return Number.isNaN(parsedHex) ? null : parsedHex
  }

  if (normalizedHue.startsWith('hue(') && normalizedHue.endsWith(')')) {
    const rangeContent = normalizedHue.slice(4, -1)
    const [minText, maxText] = rangeContent.split(':')
    const min = Number.parseInt((minText ?? '').trim(), 10)
    const max = Number.parseInt((maxText ?? '').trim(), 10)

    if (Number.isNaN(min) || Number.isNaN(max)) {
      return null
    }

    return Math.floor((min + max) / 2)
  }

  const parsed = Number.parseInt(normalizedHue, 10)
  return Number.isNaN(parsed) ? null : parsed
}

function toApproxHueColor(hue?: string): string | null {
  const hueNumber = parseHueNumber(hue)
  if (hueNumber === null) {
    return null
  }

  const h = ((hueNumber * 37) % 360 + 360) % 360
  const s = 58 + (hueNumber % 18)
  const l = 50 + (hueNumber % 8)

  return `hsl(${h} ${s}% ${l}%)`
}

function getRarityChipStyle(rarity?: string): { background: string; border: string; color: string } {
  switch ((rarity ?? '').toLowerCase()) {
    case 'common':
      return {
        background: 'rgba(148,163,184,0.12)',
        border: '1px solid rgba(148,163,184,0.28)',
        color: '#cbd5e1',
      }
    case 'uncommon':
      return {
        background: 'rgba(34,197,94,0.12)',
        border: '1px solid rgba(34,197,94,0.28)',
        color: '#86efac',
      }
    case 'rare':
      return {
        background: 'rgba(59,130,246,0.12)',
        border: '1px solid rgba(59,130,246,0.28)',
        color: '#93c5fd',
      }
    case 'epic':
      return {
        background: 'rgba(168,85,247,0.14)',
        border: '1px solid rgba(168,85,247,0.32)',
        color: '#d8b4fe',
      }
    case 'legendary':
      return {
        background: 'rgba(245,158,11,0.14)',
        border: '1px solid rgba(245,158,11,0.3)',
        color: '#fcd34d',
      }
    default:
      return {
        background: 'rgba(106,165,218,0.1)',
        border: '1px solid rgba(106,165,218,0.22)',
        color: '#9fc5e8',
      }
  }
}

export function ItemTemplateDetailsPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [template, setTemplate] = useState<ItemTemplateDetail | null>(null)
  const hueColor = toApproxHueColor(template?.hue)
  const rarityChipStyle = getRarityChipStyle(template?.rarity)

  useEffect(() => {
    if (!id) {
      setError('Missing template id.')
      setLoading(false)
      return
    }
    const templateId = id

    let mounted = true

    async function loadDetails() {
      setLoading(true)
      setError(null)

      try {
        const payload = await api.get<ItemTemplateDetail>(`/item-templates/${encodeURIComponent(templateId)}`)
        if (mounted) {
          setTemplate(payload)
        }
      } catch (fetchError) {
        if (mounted) {
          setError(fetchError instanceof Error ? fetchError.message : 'Failed to load item template details.')
        }
      } finally {
        if (mounted) {
          setLoading(false)
        }
      }
    }

    loadDetails()
    return () => {
      mounted = false
    }
  }, [id])

  return (
    <div className="flex flex-col gap-6 animate-fade-in w-full max-w-[1200px] mx-auto">
      <div className="flex items-center justify-between gap-4 flex-wrap">
        <div className="flex items-center gap-3">
          <Button
            size="sm"
            variant="flat"
            color="primary"
            className="font-mono text-xs uppercase tracking-wider"
            onPress={() => navigate('/item-templates')}
          >
            Back
          </Button>
          <div>
            <h1 className="font-cinzel font-semibold tracking-wider text-[#f9f4ed] text-lg">Item Template Details</h1>
            <p className="font-mono text-xs text-[rgba(185,187,211,0.45)] tracking-wider uppercase">{id}</p>
          </div>
        </div>
      </div>

      {loading && (
        <div className="rounded-xl border border-[rgba(106,165,218,0.15)] bg-[rgba(36,33,48,0.6)] p-8 flex items-center gap-3">
          <Spinner size="sm" color="primary" />
          <span className="font-mono text-xs uppercase tracking-wider text-[rgba(185,187,211,0.8)]">Loading details...</span>
        </div>
      )}

      {!loading && error && (
        <div className="rounded-lg border border-[rgba(239,68,68,0.28)] bg-[rgba(239,68,68,0.1)] px-4 py-3">
          <p className="font-mono text-xs uppercase tracking-wider text-[#ef4444]">{error}</p>
        </div>
      )}

      {!loading && !error && template && (
        <div className="grid grid-cols-1 lg:grid-cols-[180px_1fr] gap-5">
          <div className="rounded-xl border border-[rgba(106,165,218,0.2)] bg-[rgba(31,28,42,0.85)] p-3 h-fit">
            <div className="w-full h-36 flex items-center justify-center">
              <ItemTemplatePreview itemId={template.itemId} className="w-28 h-28" />
            </div>
          </div>

          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-3">
              <div className="rounded-lg border border-[rgba(106,165,218,0.12)] bg-[rgba(36,33,48,0.55)] px-3 py-2">
                <p className="font-mono text-[10px] tracking-wider uppercase text-[rgba(185,187,211,0.6)]">Name</p>
                <p className="font-mono text-sm text-[#f9f4ed]">{template.name || 'Unnamed'}</p>
              </div>
              <div className="rounded-lg border border-[rgba(106,165,218,0.12)] bg-[rgba(36,33,48,0.55)] px-3 py-2">
                <p className="font-mono text-[10px] tracking-wider uppercase text-[rgba(185,187,211,0.6)]">Category</p>
                <p className="font-mono text-sm text-[#f9f4ed]">{template.category || 'General'}</p>
              </div>
              <div className="rounded-lg border border-[rgba(106,165,218,0.12)] bg-[rgba(36,33,48,0.55)] px-3 py-2">
                <p className="font-mono text-[10px] tracking-wider uppercase text-[rgba(185,187,211,0.6)]">Item ID</p>
                <p className="font-mono text-sm text-[#f9f4ed]">{template.itemId}</p>
              </div>
              <div className="rounded-lg border border-[rgba(106,165,218,0.12)] bg-[rgba(36,33,48,0.55)] px-3 py-2">
                <p className="font-mono text-[10px] tracking-wider uppercase text-[rgba(185,187,211,0.6)]">Script ID</p>
                <p className="font-mono text-sm text-[#f9f4ed]">{template.scriptId || '-'}</p>
              </div>
            </div>

            <div className="grid grid-cols-2 lg:grid-cols-5 gap-3">
              <div className="rounded-lg border border-[rgba(106,165,218,0.12)] bg-[rgba(36,33,48,0.55)] px-3 py-2">
                <p className="font-mono text-[10px] tracking-wider uppercase text-[rgba(185,187,211,0.6)]">Weight</p>
                <p className="font-mono text-sm text-[#f9f4ed]">{template.weight ?? '-'}</p>
              </div>
              <div className="rounded-lg border border-[rgba(106,165,218,0.12)] bg-[rgba(36,33,48,0.55)] px-3 py-2">
                <p className="font-mono text-[10px] tracking-wider uppercase text-[rgba(185,187,211,0.6)]">Hue</p>
                <div className="flex items-center gap-2 mt-0.5">
                  <p className="font-mono text-sm text-[#f9f4ed]">{template.hue ?? '-'}</p>
                  {hueColor ? (
                    <span
                      className="inline-block w-4 h-4 rounded border border-[rgba(249,244,237,0.35)]"
                      style={{ backgroundColor: hueColor }}
                      title={`Approx hue preview ${hueColor}`}
                    />
                  ) : null}
                </div>
              </div>
              <div className="rounded-lg border border-[rgba(106,165,218,0.12)] bg-[rgba(36,33,48,0.55)] px-3 py-2">
                <p className="font-mono text-[10px] tracking-wider uppercase text-[rgba(185,187,211,0.6)]">Gold Value</p>
                <p className="font-mono text-sm text-[#f9f4ed]">{template.goldValue ?? '-'}</p>
              </div>
              <div className="rounded-lg border border-[rgba(106,165,218,0.12)] bg-[rgba(36,33,48,0.55)] px-3 py-2">
                <p className="font-mono text-[10px] tracking-wider uppercase text-[rgba(185,187,211,0.6)]">Gump ID</p>
                <p className="font-mono text-sm text-[#f9f4ed]">{template.gumpId ?? '-'}</p>
              </div>
              <div className="rounded-lg border border-[rgba(106,165,218,0.12)] bg-[rgba(36,33,48,0.55)] px-3 py-2">
                <p className="font-mono text-[10px] tracking-wider uppercase text-[rgba(185,187,211,0.6)]">Rarity</p>
                <div className="mt-1">
                  <Chip
                    size="sm"
                    variant="flat"
                    className="font-mono text-[10px] uppercase tracking-wider"
                    style={rarityChipStyle}
                  >
                    {template.rarity || 'None'}
                  </Chip>
                </div>
              </div>
            </div>

            <div className="rounded-lg border border-[rgba(106,165,218,0.12)] bg-[rgba(36,33,48,0.55)] px-3 py-3">
              <p className="font-mono text-[10px] tracking-wider uppercase text-[rgba(185,187,211,0.6)] mb-2">Description</p>
              <p className="font-mono text-xs text-[rgba(249,244,237,0.85)] leading-relaxed">{template.description || 'No description.'}</p>
            </div>

            <div className="rounded-lg border border-[rgba(106,165,218,0.12)] bg-[rgba(36,33,48,0.55)] px-3 py-3">
              <p className="font-mono text-[10px] tracking-wider uppercase text-[rgba(185,187,211,0.6)] mb-2">Tags</p>
              <div className="flex flex-wrap gap-2">
                {(template.tags ?? []).length === 0 ? (
                  <span className="font-mono text-xs text-[rgba(185,187,211,0.7)]">No tags.</span>
                ) : (
                  template.tags?.map((tag) => (
                    <button
                      key={tag}
                      type="button"
                      onClick={() => navigate(`/item-templates?tag=${encodeURIComponent(tag)}`)}
                      className="rounded-full border border-[rgba(106,165,218,0.15)] bg-[rgba(106,165,218,0.1)] px-2.5 py-1 font-mono text-[10px] tracking-wider text-[#f9f4ed] transition-colors hover:bg-[rgba(106,165,218,0.22)]"
                    >
                      {tag}
                    </button>
                  ))
                )}
              </div>
            </div>

            <div className="rounded-lg border border-[rgba(106,165,218,0.12)] bg-[rgba(36,33,48,0.55)] px-3 py-3">
              <p className="font-mono text-[10px] tracking-wider uppercase text-[rgba(185,187,211,0.6)] mb-2">Params</p>
              {Object.keys(template.params ?? {}).length === 0 ? (
                <span className="font-mono text-xs text-[rgba(185,187,211,0.7)]">No params.</span>
              ) : (
                <div className="flex flex-col gap-2">
                  {Object.entries(template.params ?? {}).map(([key, value]) => (
                    <div
                      key={key}
                      className="grid grid-cols-[1fr_auto_1fr] items-center gap-2 rounded border border-[rgba(106,165,218,0.12)] bg-[rgba(31,28,42,0.65)] px-2 py-1.5"
                    >
                      <span className="font-mono text-xs text-[#f9f4ed] break-all">{key}</span>
                      <span className="font-mono text-[10px] uppercase tracking-wider text-[#6aa5da]">
                        {itemTemplateParamTypeToLabel(value.type)}
                      </span>
                      <span className="font-mono text-xs text-[rgba(249,244,237,0.82)] break-all text-right">{value.value}</span>
                    </div>
                  ))}
                </div>
              )}
            </div>

            <div className="rounded-lg border border-[rgba(106,165,218,0.12)] bg-[rgba(36,33,48,0.55)] px-3 py-3">
              <p className="font-mono text-[10px] tracking-wider uppercase text-[rgba(185,187,211,0.6)] mb-2">
                Container Contents
              </p>
              {(template.containerItems ?? []).length === 0 ? (
                <span className="font-mono text-xs text-[rgba(185,187,211,0.7)]">
                  {(template.container ?? []).length === 0 ? 'No container contents.' : 'Container entries could not be resolved.'}
                </span>
              ) : (
                <div className="flex flex-col gap-2">
                  {template.containerItems?.map((item) => (
                    <div
                      key={item.id}
                      className="grid grid-cols-[56px_1fr_auto] items-center gap-3 rounded border border-[rgba(106,165,218,0.12)] bg-[rgba(31,28,42,0.65)] px-2 py-2"
                    >
                      <div className="flex h-12 w-12 items-center justify-center rounded border border-[rgba(106,165,218,0.12)] bg-[rgba(14,18,26,0.65)]">
                        <ItemTemplatePreview itemId={item.itemId} className="w-10 h-10" />
                      </div>
                      <div className="min-w-0">
                        <div className="font-mono text-xs text-[#f9f4ed] truncate">{item.name || 'Unnamed'}</div>
                        <div className="font-mono text-[10px] uppercase tracking-wider text-[rgba(185,187,211,0.55)]">
                          {item.id}
                        </div>
                      </div>
                      <Button
                        size="sm"
                        variant="flat"
                        color="primary"
                        className="font-mono text-[10px] uppercase tracking-wider"
                        onPress={() => navigate(`/item-templates/${encodeURIComponent(item.id)}`)}
                      >
                        Open
                      </Button>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
