import { clampWorld, facetBounds, latLngToWorld, tileUrlTemplate, worldToLatLng, type MapFacetInfo } from './maps'

const felucca: MapFacetInfo = {
  name: 'Felucca',
  width: 6144,
  height: 4096,
  maxZoom: 5,
  tileSize: 256,
  tilesAcross: 24,
  tilesDown: 16,
}

describe('maps', () => {
  describe('tileUrlTemplate', () => {
    it('keeps the Leaflet z/x/y placeholders and sets the style', () => {
      expect(tileUrlTemplate('Felucca', 'flat')).toBe('/api/v1/images/maps/Felucca/{z}/{x}/{y}.png?style=flat')
      expect(tileUrlTemplate('Felucca', 'relief')).toBe('/api/v1/images/maps/Felucca/{z}/{x}/{y}.png?style=relief')
    })

    it('encodes the facet name', () => {
      expect(tileUrlTemplate('Ter Mur', 'flat')).toBe('/api/v1/images/maps/Ter%20Mur/{z}/{x}/{y}.png?style=flat')
    })
  })

  describe('world/latlng round-trip', () => {
    it('is the identity for integer tile coordinates', () => {
      for (const [x, y] of [
        [0, 0],
        [100, 50],
        [6143, 4095],
      ] as const) {
        expect(latLngToWorld(felucca, worldToLatLng(felucca, x, y))).toEqual({ x, y })
      }
    })

    it('actually uses y (the vertical axis is not collapsed)', () => {
      expect(worldToLatLng(felucca, 0, 0).lat).not.toBe(worldToLatLng(felucca, 0, 4095).lat)
    })
  })

  describe('facetBounds', () => {
    it('contains both facet corners', () => {
      const b = facetBounds(felucca)
      expect(b.contains(worldToLatLng(felucca, 0, 0))).toBe(true)
      expect(b.contains(worldToLatLng(felucca, 6143, 4095))).toBe(true)
    })
  })

  describe('clampWorld', () => {
    it('passes an in-bounds coordinate through and flags it inside', () => {
      expect(clampWorld(felucca, 100, 200)).toEqual({ x: 100, y: 200, inBounds: true })
    })

    it('clamps an out-of-bounds coordinate to the edge and flags it outside', () => {
      expect(clampWorld(felucca, -5, 99999)).toEqual({ x: 0, y: 4095, inBounds: false })
    })

    it('treats width/height themselves as outside (tiles are 0-based)', () => {
      expect(clampWorld(felucca, 6144, 4096).inBounds).toBe(false)
    })
  })
})
