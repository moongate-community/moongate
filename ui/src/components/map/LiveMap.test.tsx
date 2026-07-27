import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import '../../lib/i18n'
import { LiveMap } from './LiveMap'
import type { MapFacetInfo } from '../../lib/maps'
import type { OnlinePlayer } from '../../lib/online-players'

const felucca: MapFacetInfo = {
  name: 'Felucca',
  width: 6144,
  height: 4096,
  maxZoom: 5,
  tileSize: 256,
  tilesAcross: 24,
  tilesDown: 16,
}

const freydis: OnlinePlayer = {
  characterSerial: '0x00000042',
  characterName: 'Freydis',
  accountSerial: '0x00000001',
  accountUsername: 'alice',
  mapId: 0,
  mapName: 'Felucca',
  x: 1420,
  y: 1698,
  z: 10,
  direction: 'North',
  running: false,
  body: 401,
  skinHue: 1002,
  hits: 48,
  hitsMax: 70,
  warmode: false,
}

function map(players: readonly OnlinePlayer[]) {
  return (
    <div style={{ width: 800, height: 600 }}>
      <LiveMap facet={felucca} style="flat" centerTarget={null} onHover={() => {}} players={players} />
    </div>
  )
}

describe('LiveMap', () => {
  it('mounts a Leaflet map and tears it down without throwing', () => {
    const { container, unmount } = render(map([]))

    expect(container.querySelector('.leaflet-container')).not.toBeNull()
    expect(() => unmount()).not.toThrow()
  })

  it('adds, updates in place, and removes a keyed player marker', async () => {
    const view = render(map([freydis]))
    const marker = await waitFor(() => {
      const element = view.container.querySelector<HTMLElement>('.mg-player-marker')
      expect(element).not.toBeNull()
      return element!
    })

    expect(marker).not.toHaveClass('mg-player-marker--warmode')
    expect(marker.querySelector<HTMLElement>('.mg-player-marker__arrow')).toHaveStyle({
      transform: 'rotate(0deg)',
    })

    view.rerender(
      map([
        {
          ...freydis,
          x: 1500,
          y: 1700,
          direction: 'East',
          warmode: true,
        },
      ]),
    )

    await waitFor(() => {
      const updated = view.container.querySelector<HTMLElement>('.mg-player-marker')
      expect(updated).toBe(marker)
      expect(updated).toHaveClass('mg-player-marker--warmode')
      expect(updated?.querySelector<HTMLElement>('.mg-player-marker__arrow')).toHaveStyle({
        transform: 'rotate(90deg)',
      })
    })

    view.rerender(map([]))
    await waitFor(() => expect(view.container.querySelector('.mg-player-marker')).toBeNull())
  })

  it('opens a complete popup without interpreting player data as HTML', async () => {
    const unsafeName = '<img src=x data-pwned=true>'
    const unsafeAccount = '<script data-pwned=true>'
    const view = render(
      map([
        {
          ...freydis,
          characterName: unsafeName,
          accountUsername: unsafeAccount,
          running: true,
          warmode: true,
        },
      ]),
    )

    const markerHost = await waitFor(() => {
      const element = view.container.querySelector<HTMLElement>('.mg-player-marker-host')
      expect(element).not.toBeNull()
      return element!
    })

    fireEvent.click(markerHost)

    expect(await screen.findByRole('heading', { name: unsafeName })).toBeInTheDocument()
    expect(
      screen.getByText(
        (content, element) =>
          element?.classList.contains('mg-player-popup__account') === true && content.includes(unsafeAccount),
      ),
    ).toBeInTheDocument()
    expect(screen.getByText('48 / 70')).toBeInTheDocument()
    expect(screen.getByText('Running')).toBeInTheDocument()
    expect(screen.getByText('War mode')).toBeInTheDocument()
    expect(view.container.querySelector('[data-pwned="true"]')).toBeNull()
  })

  it('preserves an open popup while the same serial is updated', async () => {
    const view = render(map([freydis]))
    const markerHost = await waitFor(() => {
      const element = view.container.querySelector<HTMLElement>('.mg-player-marker-host')
      expect(element).not.toBeNull()
      return element!
    })

    fireEvent.click(markerHost)
    expect(await screen.findByText('1420, 1698, 10')).toBeInTheDocument()

    view.rerender(map([{ ...freydis, x: 1421, y: 1699, z: 11 }]))

    expect(await screen.findByText('1421, 1699, 11')).toBeInTheDocument()
    expect(view.container.querySelector('.leaflet-popup')).not.toBeNull()
  })
})
