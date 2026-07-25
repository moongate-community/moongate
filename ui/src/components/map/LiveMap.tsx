import { useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import type { TFunction } from 'i18next'
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
import { directionDegrees, type OnlinePlayer } from '../../lib/online-players'

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
  /** Staff players already filtered to this facet and to valid world bounds. */
  players: readonly OnlinePlayer[]
}

function textElement<K extends keyof HTMLElementTagNameMap>(
  tag: K,
  className: string,
  text: string,
): HTMLElementTagNameMap[K] {
  const element = document.createElement(tag)
  element.className = className
  element.textContent = text
  return element
}

function playerTooltip(player: OnlinePlayer): HTMLElement {
  return textElement('span', 'mg-player-tooltip', player.characterName)
}

function addPopupRow(list: HTMLDListElement, label: string, value: string): void {
  list.append(textElement('dt', 'mg-player-popup__label', label), textElement('dd', 'mg-player-popup__value', value))
}

function playerPopup(player: OnlinePlayer, t: TFunction): HTMLElement {
  const root = document.createElement('section')
  root.className = 'mg-player-popup'

  root.append(
    textElement('h3', 'mg-player-popup__title', player.characterName),
    textElement('p', 'mg-player-popup__account', `${t('map.players.account')}: ${player.accountUsername}`),
  )

  const facts = document.createElement('dl')
  facts.className = 'mg-player-popup__facts'
  addPopupRow(facts, t('map.players.facet'), player.mapName)
  addPopupRow(facts, t('map.players.coordinates'), `${player.x}, ${player.y}, ${player.z}`)
  addPopupRow(facts, t('map.players.hits'), `${player.hits} / ${player.hitsMax}`)
  addPopupRow(facts, t('map.players.direction'), player.direction)
  root.append(facts)

  const statuses = document.createElement('div')
  statuses.className = 'mg-player-popup__statuses'
  if (player.running) {
    statuses.append(textElement('span', 'mg-player-popup__status', t('map.players.running')))
  }
  if (player.warmode) {
    statuses.append(
      textElement('span', 'mg-player-popup__status mg-player-popup__status--danger', t('map.players.warmode')),
    )
  }
  if (statuses.childElementCount > 0) {
    root.append(statuses)
  }

  const technical = document.createElement('details')
  technical.className = 'mg-player-popup__technical'
  technical.append(textElement('summary', 'mg-player-popup__summary', t('map.players.technical')))

  const technicalFacts = document.createElement('dl')
  technicalFacts.className = 'mg-player-popup__facts'
  addPopupRow(technicalFacts, t('map.players.characterSerial'), player.characterSerial)
  addPopupRow(technicalFacts, t('map.players.accountSerial'), player.accountSerial)
  addPopupRow(technicalFacts, t('map.players.body'), String(player.body))
  addPopupRow(technicalFacts, t('map.players.skinHue'), String(player.skinHue))
  technical.append(technicalFacts)
  root.append(technical)

  return root
}

function playerIcon(player: OnlinePlayer): L.DivIcon {
  const pin = document.createElement('span')
  pin.className = 'mg-player-marker'
  pin.classList.toggle('mg-player-marker--warmode', player.warmode)

  const arrow = textElement('span', 'mg-player-marker__arrow', '▲')
  arrow.style.transform = `rotate(${directionDegrees(player.direction)}deg)`
  pin.append(arrow)

  return L.divIcon({
    className: 'mg-player-marker-host',
    html: pin,
    iconSize: [28, 28],
    iconAnchor: [14, 14],
    tooltipAnchor: [0, -16],
    popupAnchor: [0, -16],
  })
}

function updatePlayerMarker(marker: L.Marker, player: OnlinePlayer, facet: MapFacetInfo, t: TFunction): void {
  marker.setLatLng(worldToLatLng(facet, player.x, player.y))

  const host = marker.getElement()
  host?.setAttribute('title', player.characterName)
  host?.setAttribute('aria-label', t('map.players.markerAlt', { name: player.characterName }))

  const pin = host?.querySelector<HTMLElement>('.mg-player-marker')
  pin?.classList.toggle('mg-player-marker--warmode', player.warmode)

  const arrow = pin?.querySelector<HTMLElement>('.mg-player-marker__arrow')
  if (arrow !== null && arrow !== undefined) {
    arrow.style.transform = `rotate(${directionDegrees(player.direction)}deg)`
  }

  marker.getTooltip()?.setContent(playerTooltip(player))
  marker.getPopup()?.setContent(playerPopup(player, t))
}

/**
 * The only imperative, Leaflet-touching piece. Every calculation lives in lib/maps.ts and
 * lib/online-players.ts; this file wires tiles, hover coordinates, and keyed staff markers.
 */
export function LiveMap({ facet, style, centerTarget, onHover, players }: LiveMapProps) {
  const { t } = useTranslation()
  const containerRef = useRef<HTMLDivElement | null>(null)
  const mapRef = useRef<L.Map | null>(null)
  const layerRef = useRef<L.TileLayer | null>(null)
  const playerLayerRef = useRef<L.LayerGroup | null>(null)
  const playerMarkersRef = useRef(new Map<string, L.Marker>())

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

    const playerLayer = L.layerGroup().addTo(map)
    playerLayerRef.current = playerLayer

    map.on('mousemove', (e: L.LeafletMouseEvent) => onHoverRef.current(latLngToWorld(facet, e.latlng)))
    map.on('mouseout', () => onHoverRef.current(null))

    mapRef.current = map
    return () => {
      playerLayer.clearLayers()
      playerMarkersRef.current.clear()
      map.remove()
      mapRef.current = null
      layerRef.current = null
      playerLayerRef.current = null
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

  // Diff by serial so marker DOM and any open popup survive a five-second position update.
  useEffect(() => {
    const playerLayer = playerLayerRef.current
    if (playerLayer === null) return

    const present = new Set(players.map((player) => player.characterSerial))

    for (const [serial, marker] of playerMarkersRef.current) {
      if (present.has(serial)) continue
      playerLayer.removeLayer(marker)
      playerMarkersRef.current.delete(serial)
    }

    for (const player of players) {
      let marker = playerMarkersRef.current.get(player.characterSerial)

      if (marker === undefined) {
        marker = L.marker(worldToLatLng(facet, player.x, player.y), {
          icon: playerIcon(player),
          keyboard: true,
          title: player.characterName,
          alt: t('map.players.markerAlt', { name: player.characterName }),
          riseOnHover: true,
        })
          .bindTooltip(playerTooltip(player), {
            direction: 'top',
            opacity: 0.95,
          })
          .bindPopup(playerPopup(player, t), {
            className: 'mg-player-popup-shell',
            maxWidth: 320,
          })
          .addTo(playerLayer)

        playerMarkersRef.current.set(player.characterSerial, marker)
      }

      updatePlayerMarker(marker, player, facet, t)
    }
  }, [facet, players, t])

  // Pan on demand. A fresh centerTarget object per request is what re-runs this (see MapScreen).
  useEffect(() => {
    if (centerTarget === null) return
    mapRef.current?.setView(worldToLatLng(facet, centerTarget.x, centerTarget.y))
  }, [facet, centerTarget])

  return <div ref={containerRef} className="size-full" />
}
