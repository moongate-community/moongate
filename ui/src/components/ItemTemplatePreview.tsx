import { useEffect, useState } from 'react'
import { Spinner } from '@heroui/react'
import { api } from '../api/client'

interface ItemTemplatePreviewProps {
  itemId: string
  className?: string
}

export function ItemTemplatePreview({ itemId, className }: ItemTemplatePreviewProps) {
  const [previewUrl, setPreviewUrl] = useState<string | null>(null)
  const [status, setStatus] = useState<'loading' | 'ok' | 'error'>('loading')

  useEffect(() => {
    let objectUrl: string | null = null
    let disposed = false

    async function loadPreview() {
      setStatus('loading')
      setPreviewUrl(null)

      try {
        const blob = await api.getBlob(`/item-templates/by-item-id/${encodeURIComponent(itemId)}/image`)
        if (disposed) {
          return
        }

        objectUrl = URL.createObjectURL(blob)
        setPreviewUrl(objectUrl)
        setStatus('ok')
      } catch {
        if (!disposed) {
          setStatus('error')
        }
      }
    }

    loadPreview()

    return () => {
      disposed = true
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl)
      }
    }
  }, [itemId])

  if (status === 'loading') {
    return (
      <div
        className={`rounded-lg border border-[rgba(106,165,218,0.15)] bg-[rgba(31,28,42,0.75)] flex items-center justify-center ${className ?? 'w-11 h-11'}`}
      >
        <Spinner size="sm" color="primary" />
      </div>
    )
  }

  if (status === 'error' || !previewUrl) {
    return (
      <div
        className={`rounded-lg border border-[rgba(106,165,218,0.15)] bg-[rgba(31,28,42,0.75)] flex items-center justify-center ${className ?? 'w-11 h-11'}`}
      >
        <span className="font-mono text-[10px] tracking-wider text-[rgba(185,187,211,0.6)]">N/A</span>
      </div>
    )
  }

  return (
    <div
      className={`rounded-lg border border-[rgba(106,165,218,0.18)] bg-[rgba(31,28,42,0.75)] overflow-hidden ${className ?? 'w-11 h-11'}`}
    >
      <img src={previewUrl} alt={itemId} className="w-full h-full object-contain p-1" loading="lazy" />
    </div>
  )
}
