import { type ReactNode, useEffect, useState } from 'react'
import { createPortal } from 'react-dom'

interface HoverPreviewState {
  x: number
  y: number
}

interface ItemImageHoverPreviewProps {
  previewSrc: string | null
  name: string
  children: ReactNode
}

export function ItemImageHoverPreview({ previewSrc, name, children }: ItemImageHoverPreviewProps) {
  const [isOpen, setIsOpen] = useState(false)
  const [position, setPosition] = useState<HoverPreviewState>({ x: 0, y: 0 })

  useEffect(() => {
    if (!isOpen) {
      return undefined
    }

    function handleWindowLeave() {
      setIsOpen(false)
    }

    window.addEventListener('blur', handleWindowLeave)

    return () => {
      window.removeEventListener('blur', handleWindowLeave)
    }
  }, [isOpen])

  return (
    <>
      <div
        onMouseEnter={() => setIsOpen(true)}
        onMouseLeave={() => setIsOpen(false)}
        onMouseMove={(event) => setPosition({ x: event.clientX + 18, y: event.clientY + 18 })}
      >
        {children}
      </div>
      {isOpen && previewSrc && typeof document !== 'undefined'
        ? createPortal(
            <div
              className="pointer-events-none fixed z-[9999] w-[212px] overflow-hidden rounded-2xl border p-3 shadow-2xl backdrop-blur-sm"
              style={{
                left: `${position.x}px`,
                top: `${position.y}px`,
                borderColor: 'rgba(214,179,106,0.22)',
                background: [
                  'radial-gradient(circle at top, rgba(214,179,106,0.12), transparent 55%)',
                  'linear-gradient(180deg, rgba(34,25,18,0.96), rgba(20,15,11,0.98))',
                ].join(', '),
                boxShadow: '0 28px 64px rgba(0,0,0,0.45)',
              }}
            >
              <div
                className="flex h-[176px] items-center justify-center rounded-xl border"
                style={{
                  borderColor: 'rgba(214,179,106,0.14)',
                  background: 'linear-gradient(180deg, rgba(214,179,106,0.08), rgba(214,179,106,0.03))',
                }}
              >
                <div className="flex h-[160px] w-[160px] items-center justify-center">
                  <img
                    src={previewSrc}
                    alt={name}
                    className="h-[160px] w-[160px] object-contain"
                    style={{ imageRendering: 'pixelated' }}
                  />
                </div>
              </div>
              <div className="mt-3 font-mono text-[11px] uppercase tracking-[0.18em]" style={{ color: '#f4d6a0' }}>
                {name}
              </div>
            </div>,
            document.body,
          )
        : null}
    </>
  )
}
