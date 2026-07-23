import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Tabs, TabsContent, TabsList, TabsTrigger } from './tabs'

describe('Tabs', () => {
  it('switches the visible panel', async () => {
    render(
      <Tabs defaultValue="status">
        <TabsList>
          <TabsTrigger value="status">Status</TabsTrigger>
          <TabsTrigger value="log">Log</TabsTrigger>
        </TabsList>
        <TabsContent value="status">status panel</TabsContent>
        <TabsContent value="log">log panel</TabsContent>
      </Tabs>,
    )

    expect(screen.getByText('status panel')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('tab', { name: 'Log' }))
    expect(screen.getByText('log panel')).toBeInTheDocument()
  })
})
