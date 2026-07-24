import { useMemo, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { Card } from '../components/ui/card'
import { Input } from '../components/ui/input'
import { Label } from '../components/ui/label'
import { Switch } from '../components/ui/switch'
import { Button } from '../components/ui/button'
import { LiveMap, type WorldCoord } from '../components/map/LiveMap'
import { clampWorld, useMaps, type MapFacetInfo, type MapStyle } from '../lib/maps'

// The default facet when the shard serves it; otherwise the first one it does serve (see below).
const PREFERRED_FACET = 'Felucca'

export function MapScreen() {
  const { t } = useTranslation()
  const maps = useMaps()

  const facets = maps.data ?? []
  const [facetName, setFacetName] = useState<string | null>(null)
  const [style, setStyle] = useState<MapStyle>('flat')
  const [hover, setHover] = useState<WorldCoord | null>(null)
  const [centerTarget, setCenterTarget] = useState<WorldCoord | null>(null)
  const [jumpX, setJumpX] = useState('')
  const [jumpY, setJumpY] = useState('')
  const [jumpError, setJumpError] = useState(false)

  // The selected facet: the user's pick, else Felucca, else the first the shard serves.
  const facet: MapFacetInfo | undefined = useMemo(() => {
    if (facets.length === 0) return undefined
    return facets.find((f) => f.name === facetName) ?? facets.find((f) => f.name === PREFERRED_FACET) ?? facets[0]
  }, [facets, facetName])

  if (maps.isPending) {
    return <p className="text-sm text-muted">{t('common.loading')}</p>
  }
  if (maps.isError || facet === undefined) {
    return (
      <p role="alert" className="text-sm text-danger-text">
        {t('map.unavailable')}
      </p>
    )
  }

  function copyHover() {
    if (hover === null) return
    const { x, y } = hover
    const write = navigator.clipboard?.writeText(`${x}, ${y}`)
    // No clipboard API (insecure context) or a rejected write: stay silent rather than claim success.
    if (write === undefined) return
    void write.then(() => toast(t('map.copied', { x, y }))).catch(() => {})
  }

  // Declared as a const arrow function (not `function submitJump`): a hoisted function declaration is
  // analyzed from scope entry, before the `facet === undefined` guard above narrows it, so TS would see
  // `facet` as possibly undefined here. An arrow function assigned after the guard closes over the
  // narrowed type instead.
  const submitJump = (e: FormEvent) => {
    e.preventDefault()
    const x = Number.parseInt(jumpX, 10)
    const y = Number.parseInt(jumpY, 10)
    if (!Number.isFinite(x) || !Number.isFinite(y) || !clampWorld(facet, x, y).inBounds) {
      setJumpError(true)
      return
    }
    setJumpError(false)
    setCenterTarget({ x, y }) // a fresh object each submit is what re-runs LiveMap's pan effect
  }

  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-xl text-ink">{t('map.title')}</h1>

      <Card className="flex flex-wrap items-end gap-4 p-4">
        <div className="flex flex-col gap-1">
          <Label htmlFor="map-facet">{t('map.facet')}</Label>
          <select
            id="map-facet"
            value={facet.name}
            onChange={(e) => {
              setFacetName(e.target.value)
              setCenterTarget(null)
              setJumpX('')
              setJumpY('')
              setJumpError(false)
            }}
            className="h-9 rounded-control border border-border-subtle bg-page px-2 text-sm text-ink"
          >
            {facets.map((f) => (
              <option key={f.name} value={f.name}>
                {f.name}
              </option>
            ))}
          </select>
        </div>

        <div className="flex items-center gap-2">
          <Switch
            id="map-relief"
            checked={style === 'relief'}
            onCheckedChange={(on) => setStyle(on ? 'relief' : 'flat')}
          />
          <Label htmlFor="map-relief">{t('map.relief')}</Label>
        </div>

        <form onSubmit={submitJump} className="flex items-end gap-2">
          <div className="flex flex-col gap-1">
            <Label htmlFor="map-x">{t('map.x')}</Label>
            <Input
              id="map-x"
              inputMode="numeric"
              value={jumpX}
              onChange={(e) => setJumpX(e.target.value)}
              className="w-24"
            />
          </div>
          <div className="flex flex-col gap-1">
            <Label htmlFor="map-y">{t('map.y')}</Label>
            <Input
              id="map-y"
              inputMode="numeric"
              value={jumpY}
              onChange={(e) => setJumpY(e.target.value)}
              className="w-24"
            />
          </div>
          <Button type="submit">{t('map.go')}</Button>
        </form>

        <button
          type="button"
          onClick={copyHover}
          title={t('map.copyHint')}
          className="ml-auto font-mono text-sm text-muted hover:text-gold"
        >
          {hover === null ? '—, —' : `${hover.x}, ${hover.y}`}
        </button>
      </Card>

      {jumpError && (
        <p role="alert" className="text-sm text-danger-text">
          {t('map.jumpError', { width: facet.width - 1, height: facet.height - 1 })}
        </p>
      )}

      <Card className="h-[70vh] overflow-hidden p-0">
        <LiveMap key={facet.name} facet={facet} style={style} centerTarget={centerTarget} onHover={setHover} />
      </Card>
    </div>
  )
}
