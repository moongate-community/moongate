import { useQuery } from '@tanstack/react-query'
import L from 'leaflet'
import { apiFetch } from './api'
import type { components } from './api-types'

// The generated schema is the single source of truth for the facet shape; re-declaring it would let the
// API drift away from the viewer silently. Same reasoning as lib/queries.ts.
export type MapFacetInfo = components['schemas']['MapFacetInfo']

export type MapStyle = 'flat' | 'relief'

// One CRS shared by the renderer AND the coordinate maths, so a hover readout can never disagree with
// where a tile is drawn. CRS.Simple maps one world pixel to one map tile at native zoom — exactly the
// backend's pyramid (zoom 0 = whole facet in one tile, maxZoom = one pixel per tile). If the rendered
// facet ever came out upside-down, this single CRS is the one knob to change.
export const MAP_CRS = L.CRS.Simple

/** The Leaflet tile-URL template for a facet + style. `{z}/{x}/{y}` stay as Leaflet placeholders. */
export function tileUrlTemplate(facet: string, style: MapStyle): string {
  return `/api/v1/images/maps/${encodeURIComponent(facet)}/{z}/{x}/{y}.png?style=${style}`
}

/** A world tile coordinate (x east, y south) as a Leaflet LatLng under {@link MAP_CRS}. */
export function worldToLatLng(info: MapFacetInfo, x: number, y: number): L.LatLng {
  return MAP_CRS.pointToLatLng(L.point(x, y), info.maxZoom)
}

/** The world tile (x,y) under a LatLng, to the nearest tile. Inverse of {@link worldToLatLng}. */
export function latLngToWorld(info: MapFacetInfo, latlng: L.LatLng): { x: number; y: number } {
  const p = MAP_CRS.latLngToPoint(latlng, info.maxZoom)
  return { x: Math.round(p.x), y: Math.round(p.y) }
}

/** The bounds covering the whole facet — for the tile layer and to fence panning. */
export function facetBounds(info: MapFacetInfo): L.LatLngBounds {
  return L.latLngBounds(worldToLatLng(info, 0, 0), worldToLatLng(info, info.width, info.height))
}

/** Clamps a coordinate into the facet and reports whether the input was already inside it. */
export function clampWorld(info: MapFacetInfo, x: number, y: number): { x: number; y: number; inBounds: boolean } {
  const inBounds = x >= 0 && y >= 0 && x < info.width && y < info.height
  return {
    x: Math.min(Math.max(x, 0), info.width - 1),
    y: Math.min(Math.max(y, 0), info.height - 1),
    inBounds,
  }
}

/** The facets this shard serves, with the grid geometry a viewer needs to configure itself. */
export const useMaps = () =>
  useQuery({ queryKey: ['maps'], queryFn: () => apiFetch<MapFacetInfo[]>('/api/v1/images/maps') })
