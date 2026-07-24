import { render } from '@testing-library/react'
import { LiveMap } from './LiveMap'
import type { MapFacetInfo } from '../../lib/maps'

const felucca: MapFacetInfo = {
  name: 'Felucca',
  width: 6144,
  height: 4096,
  maxZoom: 5,
  tileSize: 256,
  tilesAcross: 24,
  tilesDown: 16,
}

describe('LiveMap', () => {
  it('mounts a Leaflet map and tears it down without throwing', () => {
    const { container, unmount } = render(
      <div style={{ width: 800, height: 600 }}>
        <LiveMap facet={felucca} style="flat" centerTarget={null} onHover={() => {}} />
      </div>,
    )

    expect(container.querySelector('.leaflet-container')).not.toBeNull()
    expect(() => unmount()).not.toThrow()
  })
})
