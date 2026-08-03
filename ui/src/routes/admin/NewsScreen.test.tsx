import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'

import '../../lib/i18n'
import { NewsScreen } from './NewsScreen'
import type { News } from '../../lib/news'

const state = vi.hoisted(() => ({
  data: [] as News[] | undefined,
  isError: false,
  created: [] as unknown[],
  updated: [] as unknown[],
  deleted: [] as number[],
}))

vi.mock('../../lib/news', async (original) => ({
  ...(await original<typeof import('../../lib/news')>()),
  useNews: () => ({ data: state.data, isError: state.isError, isPending: false }),
  useCreateNews: () => ({ mutateAsync: async (body: unknown) => void state.created.push(body) }),
  useUpdateNews: () => ({ mutateAsync: async (body: unknown) => void state.updated.push(body) }),
  useDeleteNews: () => ({ mutateAsync: async (id: number) => void state.deleted.push(id) }),
}))

function entry(id: number, title: string, isPublished: boolean): News {
  return {
    id,
    title,
    body: 'the body',
    author: 'tom',
    publishedAt: '2026-08-03T10:00:00Z',
    updatedAt: '2026-08-03T10:00:00Z',
    isPublished,
  } as News
}

function renderWith(data: News[] | undefined, over: Partial<typeof state> = {}) {
  Object.assign(state, { data, isError: false, created: [], updated: [], deleted: [] }, over)

  return render(
    <QueryClientProvider client={new QueryClient()}>
      <NewsScreen />
    </QueryClientProvider>,
  )
}

describe('NewsScreen', () => {
  it('lists every entry, drafts included', () => {
    renderWith([entry(1, 'Maintenance', true), entry(2, 'Still cooking', false)])

    expect(screen.getByText('Maintenance')).toBeInTheDocument()
    expect(screen.getByText('Still cooking')).toBeInTheDocument()
  })

  it('tells a draft from a published entry', () => {
    renderWith([entry(1, 'Maintenance', true), entry(2, 'Still cooking', false)])

    // "Published" is now both a stat-card label and a row badge, so the row is what this asserts.
    expect(screen.getAllByText('Published').length).toBeGreaterThan(1)
    expect(screen.getByText('Draft')).toBeInTheDocument()
  })

  it('opens an empty dialog for a new entry', async () => {
    renderWith([entry(1, 'Maintenance', true)])

    await userEvent.click(screen.getByRole('button', { name: 'New entry' }))

    expect(screen.getByLabelText('Title')).toHaveValue('')
  })

  it('opens the dialog filled in for an existing entry', async () => {
    renderWith([entry(1, 'Maintenance', true)])

    await userEvent.click(screen.getByRole('button', { name: 'Edit' }))

    expect(screen.getByLabelText('Title')).toHaveValue('Maintenance')
  })

  // Publishing interrupts everyone in the world, so the dialog says so before it happens — and only
  // when it is actually about to happen.
  it('warns before an entry is published for the first time', async () => {
    renderWith([entry(2, 'Still cooking', false)])

    await userEvent.click(screen.getByRole('button', { name: 'Edit' }))

    expect(screen.queryByText(/announces the title/)).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('switch'))

    expect(screen.getByText(/announces the title/)).toBeInTheDocument()
  })

  it('does not warn when the entry is already published', async () => {
    renderWith([entry(1, 'Maintenance', true)])

    await userEvent.click(screen.getByRole('button', { name: 'Edit' }))

    expect(screen.queryByText(/announces the title/)).not.toBeInTheDocument()
  })

  // Deleting is not reversible, so it asks first.
  it('asks before deleting', async () => {
    const confirm = vi.spyOn(globalThis, 'confirm').mockReturnValue(false)

    renderWith([entry(1, 'Maintenance', true)])

    await userEvent.click(screen.getByRole('button', { name: 'Delete' }))

    expect(confirm).toHaveBeenCalled()
    expect(state.deleted).toEqual([])

    confirm.mockRestore()
  })

  it('deletes once confirmed', async () => {
    const confirm = vi.spyOn(globalThis, 'confirm').mockReturnValue(true)

    renderWith([entry(1, 'Maintenance', true)])

    await userEvent.click(screen.getByRole('button', { name: 'Delete' }))

    expect(state.deleted).toEqual([1])

    confirm.mockRestore()
  })

  it('says so when the request failed', () => {
    renderWith(undefined, { isError: true })

    expect(screen.getByRole('alert')).toHaveTextContent('The news could not be loaded.')
  })

  it('says so when there is nothing yet', () => {
    renderWith([])

    expect(screen.getByText('No news yet')).toBeInTheDocument()
  })
})
