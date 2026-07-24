import { useEffect, useRef } from 'react'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import {
  facetBounds,
  latLngToWorld,
  MAP_CRS,
  tileUrlTemplate,
  worldToLatLng,
  type MapFacetInfo,
  type MapStyle,
} from '../../lib/maps'

export type WorldCoord = { x: number; y: number }

export type LiveMapProps = {
  /** The facet to render. Set the React `key` to `facet.name` so a new facet gets a fresh map. */
  facet: MapFacetInfo
  /** Which render the tiles use. Swapping this replaces the tile layer in place. */
  style: MapStyle
  /** When this becomes a *new* object, the map pans to center on it. Null does nothing. */
  centerTarget: WorldCoord | null
  /** The tile under the cursor, or null when the cursor leaves the map. */
  onHover: (coord: WorldCoord | null) => void
}

/**
 * The only imperative, Leaflet-touching piece. Every calculation lives in lib/maps.ts (and is unit-tested
 * there); this file is wiring: create the map, keep one tile layer, translate mouse events to world tiles.
 */
export function LiveMap({ facet, style, centerTarget, onHover }: LiveMapProps) {
  const containerRef = useRef<HTMLDivElement | null>(null)
  const mapRef = useRef<L.Map | null>(null)
  const layerRef = useRef<L.TileLayer | null>(null)

  // Read the latest onHover through a ref so changing the callback never re-subscribes the map events.
  const onHoverRef = useRef(onHover)
  onHoverRef.current = onHover

  // Create the map once. The facet fixes maxZoom and bounds, which a live map cannot change, so a facet
  // change remounts this component (the caller sets key={facet.name}) rather than mutating the map.
  useEffect(() => {
    const container = containerRef.current
    if (container === null) return

    const bounds = facetBounds(facet)
    const map = L.map(container, {
      crs: MAP_CRS,
      minZoom: 0,
      maxZoom: facet.maxZoom,
      maxBounds: bounds,
      maxBoundsViscosity: 1,
      attributionControl: false,
    })
    map.fitBounds(bounds)

    map.on('mousemove', (e: L.LeafletMouseEvent) => onHoverRef.current(latLngToWorld(facet, e.latlng)))
    map.on('mouseout', () => onHoverRef.current(null))

    mapRef.current = map
    return () => {
      map.remove()
      mapRef.current = null
      layerRef.current = null
    }
  }, [facet])

  // The tile layer follows facet + style. Separate from map creation so a style flip is a cheap swap.
  useEffect(() => {
    const map = mapRef.current
    if (map === null) return

    layerRef.current?.remove()
    const layer = L.tileLayer(tileUrlTemplate(facet.name, style), {
      tileSize: facet.tileSize,
      minZoom: 0,
      maxZoom: facet.maxZoom,
      noWrap: true,
      bounds: facetBounds(facet),
    })
    layer.addTo(map)
    layerRef.current = layer
  }, [facet, style])

  // Pan on demand. A fresh centerTarget object per request is what re-runs this (see MapScreen).
  useEffect(() => {
    if (centerTarget === null) return
    mapRef.current?.setView(worldToLatLng(facet, centerTarget.x, centerTarget.y))
  }, [facet, centerTarget])

  return <div ref={containerRef} className="size-full" />
}
