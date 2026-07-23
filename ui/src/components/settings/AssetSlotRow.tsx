import { useRef, useState, type ChangeEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Upload, X } from 'lucide-react'
import { Button } from '../ui/button'
import { toast } from '../ui/sonner'
import { ApiError } from '../../lib/api'
import { useDeleteAsset, useUploadAsset } from '../../lib/settings'

interface AssetSlotRowProps {
  slot: string
  label: string
  /** The public URL of the currently stored image, or undefined when the slot is empty. */
  url?: string
}

export function AssetSlotRow({ slot, label, url }: AssetSlotRowProps) {
  const { t } = useTranslation()
  const upload = useUploadAsset()
  const remove = useDeleteAsset()
  const input = useRef<HTMLInputElement>(null)

  // The public URL is stable per slot, so a re-upload keeps the same src; bump a counter to force the
  // preview to reload the fresh bytes instead of the browser's cached copy.
  const [reloads, setReloads] = useState(0)
  const previewSrc = url ? `${url}?v=${reloads}` : undefined

  function reportError(error: unknown) {
    if (error instanceof ApiError && error.status === 415) toast.error(t('admin.settings.notImage'))
    else if (error instanceof ApiError && error.status === 413) toast.error(t('admin.settings.tooLarge'))
    else toast.error(t('error.generic'))
  }

  async function onFile(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    event.target.value = '' // let the same file be re-selected after a failure
    if (!file) return
    try {
      await upload.mutateAsync({ slot, file })
      setReloads((n) => n + 1)
      toast.success(t('admin.settings.assetUpdated'))
    } catch (error) {
      reportError(error)
    }
  }

  async function onRemove() {
    try {
      await remove.mutateAsync(slot)
      toast.success(t('admin.settings.assetRemoved'))
    } catch (error) {
      reportError(error)
    }
  }

  return (
    <div className="flex items-center gap-4">
      <div className="flex h-16 w-16 shrink-0 items-center justify-center overflow-hidden rounded-control border border-border-subtle bg-deep">
        {previewSrc ? (
          <img src={previewSrc} alt={label} className="max-h-full max-w-full object-contain" />
        ) : (
          <span className="text-xs text-muted">—</span>
        )}
      </div>

      <div className="flex-1">
        <p className="text-sm font-bold text-ink">{label}</p>
      </div>

      <input
        ref={input}
        type="file"
        accept="image/*"
        onChange={onFile}
        data-testid={`asset-input-${slot}`}
        className="hidden"
      />
      <Button type="button" variant="outline" onClick={() => input.current?.click()} disabled={upload.isPending}>
        <Upload className="size-4" />
        {t('admin.settings.upload')}
      </Button>
      {url ? (
        <Button type="button" variant="ghost" onClick={onRemove} disabled={remove.isPending}>
          <X className="size-4" />
          {t('admin.settings.remove')}
        </Button>
      ) : null}
    </div>
  )
}
