import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

import '../../lib/i18n'
import { TooltipProvider } from '../ui/tooltip'
import { ItemTooltip } from './ItemTooltip'
import type { ItemTooltip as Tooltip } from '../../lib/characters'

const query = vi.hoisted(() => ({
  data: undefined as Tooltip | undefined,
  isPending: false,
  enabledWhenCalled: [] as boolean[],
}))

vi.mock('../../lib/characters', async (original) => ({
  ...(await original<typeof import('../../lib/characters')>()),
  useItemTooltip: (_character: string, _item: string, enabled: boolean) => {
    query.enabledWhenCalled.push(enabled)
    return query
  },
}))

function tooltip(over: Partial<Tooltip> = {}): Tooltip {
  return {
    serial: '0x4000000A',
    lines: ['a dagger', 'Weight: 1 stone'],
    templateId: 'dagger',
    itemId: 0x13b9,
    hue: 0,
    amount: 1,
    ...over,
  } as Tooltip
}

function renderWith(data: Tooltip | undefined, state: Partial<typeof query> = {}) {
  Object.assign(query, { data, isPending: false }, state)
  query.enabledWhenCalled = []

  return render(
    <TooltipProvider>
      <ItemTooltip characterSerial="0x1" itemSerial="0x4000000A">
        <button type="button">the item</button>
      </ItemTooltip>
    </TooltipProvider>,
  )
}

describe('ItemTooltip', () => {
  // The point of fetching on hover: a page with forty rows must make no tooltip requests until one
  // is opened. An always-enabled hook passes every other test in this file and fails this one.
  it('does not ask for anything until it opens', () => {
    renderWith(tooltip())

    expect(query.enabledWhenCalled).toEqual([false])
  })

  it('asks once it opens', async () => {
    renderWith(tooltip())

    await userEvent.tab()

    expect(query.enabledWhenCalled).toContain(true)
  })

  it('shows the lines the game reports', async () => {
    renderWith(tooltip())

    await userEvent.tab()

    expect(await screen.findByText('a dagger')).toBeInTheDocument()
    expect(screen.getByText('Weight: 1 stone')).toBeInTheDocument()
  })

  it('shows the technical fields', async () => {
    renderWith(tooltip({ templateId: 'dagger', hue: 0x21, amount: 5 }))

    await userEvent.tab()

    expect(await screen.findByText('dagger')).toBeInTheDocument()
    expect(screen.getByText('0x4000000A')).toBeInTheDocument()
  })

  // A shard with no client files describes nothing, and an empty card is worse than a sentence.
  it('says so when the game describes nothing', async () => {
    renderWith(tooltip({ lines: [] }))

    await userEvent.tab()

    expect(await screen.findByText('No description available')).toBeInTheDocument()
  })

  it('says it is loading while the request is in flight', async () => {
    renderWith(undefined, { isPending: true })

    await userEvent.tab()

    expect(await screen.findByText('Loading…')).toBeInTheDocument()
  })
})
