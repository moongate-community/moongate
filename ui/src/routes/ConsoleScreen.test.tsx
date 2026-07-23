import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import '../lib/i18n'
import type { ConsoleLine } from '../lib/console'
import { ConsoleScreen } from './ConsoleScreen'

const state = vi.hoisted(() => ({
  status: 'connected' as 'connecting' | 'connected' | 'closed',
  lines: [] as ConsoleLine[],
  send: vi.fn(),
  clear: vi.fn(),
}))

vi.mock('../lib/console', () => ({ useConsole: () => state }))
vi.mock('../lib/auth', () => ({ useSession: () => ({ username: 'admin' }) }))

describe('ConsoleScreen', () => {
  beforeEach(() => {
    state.status = 'connected'
    state.lines = []
    state.send = vi.fn()
    state.clear = vi.fn()
  })

  it('renders the output lines', () => {
    state.lines = [
      { id: 0, kind: 'command', text: 'who' },
      { id: 1, kind: 'output', text: '2 players online' },
    ]
    render(<ConsoleScreen />)

    expect(screen.getByText('2 players online')).toBeInTheDocument()
    expect(screen.getByText(/who/)).toBeInTheDocument()
  })

  it('sends the typed command on Enter and clears the input', async () => {
    render(<ConsoleScreen />)
    const input = screen.getByRole('textbox')

    await userEvent.type(input, 'save{Enter}')

    expect(state.send).toHaveBeenCalledWith('save')
    expect(input).toHaveValue('')
  })

  it('recalls the previous command with ArrowUp', async () => {
    render(<ConsoleScreen />)
    const input = screen.getByRole('textbox')

    await userEvent.type(input, 'who{Enter}')
    await userEvent.type(input, '{ArrowUp}')

    expect(input).toHaveValue('who')
  })

  it('runs the command with the Run button', async () => {
    render(<ConsoleScreen />)
    await userEvent.type(screen.getByRole('textbox'), 'broadcast hi')
    await userEvent.click(screen.getByRole('button', { name: /run/i }))

    expect(state.send).toHaveBeenCalledWith('broadcast hi')
  })

  it('fills the input from a quick-command snippet', async () => {
    render(<ConsoleScreen />)
    await userEvent.click(screen.getByRole('button', { name: /broadcast <message>/i }))

    expect(screen.getByRole('textbox')).toHaveValue('broadcast <message>')
  })

  it('clears the output with the Clear button', async () => {
    render(<ConsoleScreen />)

    await userEvent.click(screen.getByRole('button', { name: /clear/i }))

    expect(state.clear).toHaveBeenCalled()
  })

  it('disables input until connected', () => {
    state.status = 'connecting'
    render(<ConsoleScreen />)

    expect(screen.getByRole('textbox')).toBeDisabled()
  })
})
