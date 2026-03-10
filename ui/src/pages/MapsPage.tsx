import { useEffect, useRef, useState } from 'react'
import { Button, Spinner } from '@heroui/react'
import { TransformWrapper, TransformComponent } from 'react-zoom-pan-pinch'
import { rawApiFetch } from '../api/client'

const MAPS = [
  { id: 0, name: 'Felucca',  width: 7168, height: 4096 },
  { id: 1, name: 'Trammel',  width: 7168, height: 4096 },
  { id: 2, name: 'Ilshenar', width: 2304, height: 1600 },
  { id: 3, name: 'Malas',    width: 2560, height: 2048 },
  { id: 4, name: 'Tokuno',   width: 1448, height: 1448 },
  { id: 5, name: 'TerMur',   width: 1280, height: 4096 },
]

export function MapsPage() {
  const [selectedMapId, setSelectedMapId] = useState(0)
  const [imageUrl, setImageUrl] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const blobUrlRef = useRef<string | null>(null)

  useEffect(() => {
    let cancelled = false

    async function load() {
      setLoading(true)
      setError(null)
      setImageUrl(null)

      if (blobUrlRef.current) {
        URL.revokeObjectURL(blobUrlRef.current)
        blobUrlRef.current = null
      }

      try {
        const res = await rawApiFetch(`/api/maps/${selectedMapId}.png`)
        const blob = await res.blob()

        if (!cancelled) {
          const url = URL.createObjectURL(blob)
          blobUrlRef.current = url
          setImageUrl(url)
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Failed to load map image.')
        }
      } finally {
        if (!cancelled) {
          setLoading(false)
        }
      }
    }

    load()

    return () => {
      cancelled = true
    }
  }, [selectedMapId])

  useEffect(() => {
    return () => {
      if (blobUrlRef.current) {
        URL.revokeObjectURL(blobUrlRef.current)
      }
    }
  }, [])

  const selectedMap = MAPS.find((m) => m.id === selectedMapId)!

  return (
    <div className="flex flex-col gap-6 animate-fade-in w-full h-full">
      {/* Page header */}
      <div>
        <div className="flex items-center gap-3 mb-1">
          <div
            style={{
              width: '2px',
              height: '20px',
              background: '#6aa5da',
              borderRadius: '1px',
              boxShadow: '0 0 6px rgba(106,165,218,0.5)',
            }}
          />
          <h1
            className="font-cinzel font-semibold tracking-wider"
            style={{ color: '#f9f4ed', fontSize: '18px', letterSpacing: '0.12em' }}
          >
            World Maps
          </h1>
        </div>
        <p className="font-mono text-xs pl-5" style={{ color: 'rgba(185,187,211,0.35)', letterSpacing: '0.1em' }}>
          RADAR-COLOR MAP VIEWER · SCROLL TO ZOOM · DRAG TO PAN
        </p>
      </div>

      {/* Map selector */}
      <div className="flex flex-wrap gap-2">
        {MAPS.map((map) => {
          const isActive = map.id === selectedMapId
          return (
            <button
              key={map.id}
              onClick={() => setSelectedMapId(map.id)}
              className="px-4 py-1.5 rounded font-mono text-xs tracking-wider uppercase transition-all duration-150"
              style={
                isActive
                  ? {
                      background: 'rgba(106,165,218,0.18)',
                      border: '1px solid rgba(106,165,218,0.5)',
                      color: '#6aa5da',
                      boxShadow: '0 0 8px rgba(106,165,218,0.15)',
                    }
                  : {
                      background: 'rgba(36,33,48,0.6)',
                      border: '1px solid rgba(106,165,218,0.15)',
                      color: 'rgba(185,187,211,0.7)',
                    }
              }
            >
              {map.name}
            </button>
          )
        })}
      </div>

      {/* Viewer */}
      <div
        className="flex-1 rounded-xl overflow-hidden relative"
        style={{
          border: '1px solid rgba(106,165,218,0.15)',
          background: '#0a0a10',
          minHeight: '400px',
        }}
      >
        {loading && (
          <div className="absolute inset-0 flex items-center justify-center z-10">
            <div className="flex flex-col items-center gap-3">
              <Spinner color="primary" size="lg" />
              <p className="font-mono text-xs tracking-wider" style={{ color: 'rgba(185,187,211,0.5)' }}>
                GENERATING MAP IMAGE…
              </p>
            </div>
          </div>
        )}

        {error && (
          <div className="absolute inset-0 flex items-center justify-center z-10 p-6">
            <div className="rounded-lg border border-[rgba(239,68,68,0.28)] bg-[rgba(239,68,68,0.1)] px-4 py-3">
              <p className="font-mono text-xs uppercase tracking-wider text-[#ef4444]">Error: {error}</p>
            </div>
          </div>
        )}

        {imageUrl && (
          <TransformWrapper
            initialScale={1}
            minScale={0.05}
            maxScale={8}
            wheel={{ step: 0.1 }}
            centerOnInit
          >
            {({ resetTransform }) => (
              <>
                <div className="absolute top-3 right-3 z-10 flex items-center gap-2">
                  <span className="font-mono text-xs" style={{ color: 'rgba(185,187,211,0.4)' }}>
                    {selectedMap.width} × {selectedMap.height}
                  </span>
                  <Button
                    size="sm"
                    variant="flat"
                    className="font-mono text-xs uppercase tracking-wider"
                    style={{
                      background: 'rgba(36,33,48,0.85)',
                      border: '1px solid rgba(106,165,218,0.25)',
                      color: '#6aa5da',
                    }}
                    onPress={() => resetTransform()}
                  >
                    Reset
                  </Button>
                </div>

                <TransformComponent
                  wrapperStyle={{ width: '100%', height: '100%', cursor: 'grab' }}
                  contentStyle={{ width: '100%', height: '100%' }}
                >
                  <img
                    src={imageUrl}
                    alt={`${selectedMap.name} map`}
                    style={{ display: 'block', maxWidth: '100%', maxHeight: '100%' }}
                    draggable={false}
                  />
                </TransformComponent>
              </>
            )}
          </TransformWrapper>
        )}
      </div>
    </div>
  )
}
