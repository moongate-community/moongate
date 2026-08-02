import type { ReactElement } from 'react'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'

import '../../lib/i18n'
import { TooltipProvider } from '../ui/tooltip'
import { InventoryTree } from './InventoryTree'
import type { CharacterItem } from '../../lib/characters'

function item(name: string, over: Partial<CharacterItem> = {}): CharacterItem {
  return {
    serial: '0x1',
    name,
    templateId: 'sword',
    itemId: 0x13b9,
    hue: 0,
    amount: 1,
    layer: null,
    contents: [],
    ...over,
  } as CharacterItem
}

// The rows now hold a query hook and a tooltip, so they need both providers around them.
function renderTree(ui: ReactElement) {
  return render(
    <QueryClientProvider client={new QueryClient()}>
      <TooltipProvider>{ui}</TooltipProvider>
    </QueryClientProvider>,
  )
}

describe('InventoryTree', () => {
  it('shows each item with its art', () => {
    renderTree(<InventoryTree characterSerial="0x1" items={[item('Sword')]} emptyLabel="empty" />)

    expect(screen.getByAltText('Sword')).toHaveAttribute('src', '/api/v1/images/items/0x13b9.png')
  })

  it('hues the art when the item is dyed', () => {
    renderTree(
      <InventoryTree
        characterSerial="0x1"
        items={[item('Robe', { serial: '0x2', itemId: 0x1f03, hue: 0x21 })]}
        emptyLabel="empty"
      />,
    )

    expect(screen.getByAltText('Robe')).toHaveAttribute('src', '/api/v1/images/items/0x1f03.png?hue=0x21')
  })

  it('shows what a container holds', () => {
    renderTree(
      <InventoryTree
        characterSerial="0x1"
        items={[item('Bag', { serial: '0x2', contents: [item('Potion', { serial: '0x3' })] })]}
        emptyLabel="empty"
      />,
    )

    expect(screen.getByText('Bag')).toBeInTheDocument()
    expect(screen.getByText('Potion')).toBeInTheDocument()
  })

  it('shows the amount only when there is more than one', () => {
    renderTree(
      <InventoryTree
        characterSerial="0x1"
        items={[item('Gold', { serial: '0x2', amount: 250 }), item('Sword', { serial: '0x3' })]}
        emptyLabel="empty"
      />,
    )

    expect(screen.getByText('×250')).toBeInTheDocument()
    expect(screen.queryByText('×1')).not.toBeInTheDocument()
  })

  // UO names most items by cliloc rather than by stored text, so a blank name is normal here and
  // must not render as an empty row.
  it('labels an item with no stored name', () => {
    renderTree(<InventoryTree characterSerial="0x1" items={[item('')]} emptyLabel="empty" />)

    expect(screen.getByText('Unnamed item')).toBeInTheDocument()
  })

  it('names the layer of a worn item', () => {
    renderTree(
      <InventoryTree characterSerial="0x1" items={[item('Robe', { layer: 'OuterTorso' })]} emptyLabel="empty" />,
    )

    expect(screen.getByText('OuterTorso')).toBeInTheDocument()
  })

  it('says so when there is nothing', () => {
    renderTree(<InventoryTree characterSerial="0x1" items={[]} emptyLabel="Nothing here" />)

    expect(screen.getByText('Nothing here')).toBeInTheDocument()
  })
})
