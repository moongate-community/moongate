import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

import '../../lib/i18n'
import { TooltipProvider } from '../ui/tooltip'
import { ItemTemplateTooltip } from './ItemTemplateTooltip'
import type { ItemTemplateSummary } from '../../lib/templates'

function template(over: Partial<ItemTemplateSummary> = {}): ItemTemplateSummary {
  return {
    id: 'plate_mail',
    name: 'Plate Mail',
    category: 'armor',
    itemId: 0x1415,
    hue: 0,
    rarity: 'Rare',
    goldValue: 250,
    weight: 40,
    tags: ['armor', 'metal'],
    imageUrl: '/api/v1/images/items/0x1415.png',
    ...over,
  } as ItemTemplateSummary
}

function renderCard(summary: ItemTemplateSummary) {
  return render(
    <TooltipProvider>
      <ItemTemplateTooltip template={summary}>
        <button type="button">the row</button>
      </ItemTemplateTooltip>
    </TooltipProvider>,
  )
}

describe('ItemTemplateTooltip', () => {
  it('shows the name, the rarity and the numbers', async () => {
    renderCard(template())

    await userEvent.tab()

    expect(await screen.findByText('Plate Mail')).toBeInTheDocument()
    expect(screen.getByText('Rare')).toBeInTheDocument()
    expect(screen.getByText('40')).toBeInTheDocument()
    expect(screen.getByText('250')).toBeInTheDocument()
  })

  it('shows the sprite the row already carries', async () => {
    renderCard(template())

    await userEvent.tab()

    expect(await screen.findByAltText('Plate Mail')).toHaveAttribute('src', '/api/v1/images/items/0x1415.png')
  })

  it('shows the tags', async () => {
    renderCard(template({ tags: ['armor', 'metal'] }))

    await userEvent.tab()

    expect(await screen.findByText('armor, metal')).toBeInTheDocument()
  })

  it('omits the tag row when there are none', async () => {
    renderCard(template({ tags: [] }))

    await userEvent.tab()

    await screen.findByText('Plate Mail')
    expect(screen.queryByText('Tags')).not.toBeInTheDocument()
  })

  // The card renders from the row it was handed. If this ever needs a QueryClientProvider, someone
  // has added a fetch to it — and that is one request per hover for data already on screen.
  it('needs no query client, because it fetches nothing', async () => {
    renderCard(template())

    await userEvent.tab()

    expect(await screen.findByText('Plate Mail')).toBeInTheDocument()
  })

  it('falls back to the id when the template has no name', async () => {
    renderCard(template({ name: '' }))

    await userEvent.tab()

    expect(await screen.findByText('plate_mail')).toBeInTheDocument()
  })
})
