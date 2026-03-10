import { useEffect, useRef, useState, useCallback } from 'react'
import { Button, Spinner } from '@heroui/react'
import { TransformWrapper, TransformComponent } from 'react-zoom-pan-pinch'
import { rawApiFetch, api } from '../api/client'

interface ActiveSession {
  sessionId: number
  characterName: string
  mapId: number
  x: number
  y: number
}

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
  const containerRef = useRef<HTMLDivElement>(null)
  const [mousePos, setMousePos] = useState<{ x: number; y: number } | null>(null)
  const [players, setPlayers] = useState<ActiveSession[]>([])

  const handleMouseMove = useCallback((e: React.MouseEvent<HTMLDivElement>) => {
    if (!containerRef.current) return
    const rect = containerRef.current.getBoundingClientRect()
    setMousePos({ x: e.clientX - rect.left, y: e.clientY - rect.top })
  }, [])

  const handleMouseLeave = useCallback(() => {
    setMousePos(null)
  }, [])

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

  useEffect(() => {
    let mounted = true

    async function pollPlayers() {
      try {
        const sessions = await api.get<ActiveSession[]>('/sessions/active')
        if (mounted) setPlayers(sessions)
      } catch {
        // ignore polling errors silently
      }
    }

    pollPlayers()
    const id = window.setInterval(pollPlayers, 5000)
    return () => {
      mounted = false
      window.clearInterval(id)
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
        ref={containerRef}
        className="flex-1 rounded-xl overflow-hidden relative"
        style={{
          border: '1px solid rgba(106,165,218,0.15)',
          background: '#0a0a10',
          minHeight: '400px',
        }}
        onMouseMove={handleMouseMove}
        onMouseLeave={handleMouseLeave}
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
            {({ resetTransform, instance }) => {
              const { scale, positionX, positionY } = instance.transformState
              const mapX = mousePos ? Math.max(0, Math.min(selectedMap.width - 1, Math.round((mousePos.x - positionX) / scale))) : 0
              const mapY = mousePos ? Math.max(0, Math.min(selectedMap.height - 1, Math.round((mousePos.y - positionY) / scale))) : 0

              const visiblePlayers = players.filter((p) => p.mapId === selectedMapId)

              return (
              <>
                {/* Player markers */}
                {visiblePlayers.map((p) => {
                  const sx = positionX + p.x * scale
                  const sy = positionY + p.y * scale
                  return (
                    <div
                      key={p.sessionId}
                      style={{
                        position: 'absolute',
                        left: sx,
                        top: sy,
                        transform: 'translate(-50%, -50%)',
                        pointerEvents: 'none',
                        zIndex: 25,
                      }}
                    >
                      {/* Dot */}
                      <div style={{
                        width: '8px',
                        height: '8px',
                        borderRadius: '50%',
                        background: '#22c55e',
                        boxShadow: '0 0 6px #22c55e',
                        margin: '0 auto',
                      }} />
                      {/* Name */}
                      <div style={{
                        marginTop: '3px',
                        background: 'rgba(10,10,16,0.85)',
                        border: '1px solid rgba(34,197,94,0.4)',
                        borderRadius: '3px',
                        padding: '1px 6px',
                        fontFamily: 'monospace',
                        fontSize: '10px',
                        color: '#22c55e',
                        whiteSpace: 'nowrap',
                        textAlign: 'center',
                      }}>
                        {p.characterName || '?'}
                      </div>
                    </div>
                  )
                })}

                {/* Crosshair */}
                {mousePos && (
                  <>
                    <div style={{
                      position: 'absolute', left: mousePos.x, top: 0, bottom: 0,
                      width: '1px', background: 'rgba(106,165,218,0.4)',
                      pointerEvents: 'none', zIndex: 20,
                    }} />
                    <div style={{
                      position: 'absolute', top: mousePos.y, left: 0, right: 0,
                      height: '1px', background: 'rgba(106,165,218,0.4)',
                      pointerEvents: 'none', zIndex: 20,
                    }} />
                    <div style={{
                      position: 'absolute',
                      left: mousePos.x + 12,
                      top: mousePos.y + 12,
                      pointerEvents: 'none', zIndex: 20,
                      background: 'rgba(10,10,16,0.88)',
                      border: '1px solid rgba(106,165,218,0.35)',
                      borderRadius: '3px',
                      padding: '2px 8px',
                      fontFamily: 'monospace',
                      fontSize: '11px',
                      color: '#6aa5da',
                      letterSpacing: '0.08em',
                      whiteSpace: 'nowrap',
                    }}>
                      {mapX} , {mapY}
                    </div>
                  </>
                )}

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
            )
            }}
          </TransformWrapper>
        )}
      </div>
    </div>
  )
}
