import { useEffect, useRef, useState } from 'react'
import { Terminal } from '@xterm/xterm'
import { FitAddon } from '@xterm/addon-fit'
import '@xterm/xterm/css/xterm.css'
import { api } from '../api/client'

interface CommandExecuteResponse {
  success: boolean
  command: string
  outputLines: string[]
  timestamp: number
}

const PROMPT = 'moongate@server:~$ '

export function ConsolePage() {
  const [command, setCommand] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [history, setHistory] = useState<string[]>([])
  const historyIndexRef = useRef<number | null>(null)

  const terminalContainerRef = useRef<HTMLDivElement | null>(null)
  const terminalRef = useRef<Terminal | null>(null)
  const fitAddonRef = useRef<FitAddon | null>(null)

  useEffect(() => {
    if (terminalContainerRef.current === null || terminalRef.current !== null) {
      return
    }

    const terminal = new Terminal({
      convertEol: true,
      cursorBlink: true,
      fontFamily: 'JetBrains Mono, monospace',
      fontSize: 13,
      lineHeight: 1.35,
      theme: {
        background: '#0a0f14',
        foreground: '#cfd8dc',
        cursor: '#6aa5da',
        selectionBackground: 'rgba(106,165,218,0.28)',
        red: '#ef5350',
        green: '#66bb6a',
        yellow: '#ffd54f',
        blue: '#64b5f6',
        magenta: '#ba68c8',
        cyan: '#4dd0e1',
        white: '#eceff1',
        brightWhite: '#ffffff',
      },
      disableStdin: true,
      scrollback: 2000,
    })

    const fitAddon = new FitAddon()
    terminal.loadAddon(fitAddon)
    terminal.open(terminalContainerRef.current)
    fitAddon.fit()

    terminal.writeln('\x1b[1;36mMoongate Remote Console\x1b[0m')
    terminal.writeln('\x1b[90mType a command and press Enter.\x1b[0m')
    terminal.writeln('')

    const onResize = () => fitAddon.fit()
    window.addEventListener('resize', onResize)

    terminalRef.current = terminal
    fitAddonRef.current = fitAddon

    return () => {
      window.removeEventListener('resize', onResize)
      terminal.dispose()
      terminalRef.current = null
      fitAddonRef.current = null
    }
  }, [])

  useEffect(() => {
    fitAddonRef.current?.fit()
  }, [isSubmitting])

  async function executeCommand() {
    const normalizedCommand = command.trim()
    if (normalizedCommand.length === 0 || isSubmitting) {
      return
    }

    const terminal = terminalRef.current

    terminal?.writeln(`\x1b[1;34m${PROMPT}\x1b[0m${normalizedCommand}`)
    setIsSubmitting(true)

    try {
      const response = await api.post<CommandExecuteResponse>('/commands/execute', { command: normalizedCommand })

      const lines = response.outputLines.length > 0 ? response.outputLines : ['(no output)']
      const lineColor = response.success ? '\x1b[37m' : '\x1b[1;31m'

      for (const line of lines) {
        terminal?.writeln(`${lineColor}${line}\x1b[0m`)
      }

      const timestamp = new Date(response.timestamp).toLocaleTimeString()
      terminal?.writeln(`\x1b[90m[${timestamp}] exit=${response.success ? '0' : '1'}\x1b[0m`)
      terminal?.writeln('')

      setHistory((current) => {
        if (current[0] === normalizedCommand) {
          return current
        }

        return [normalizedCommand, ...current].slice(0, 100)
      })
      historyIndexRef.current = null
      setCommand('')
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Command execution failed.'
      terminal?.writeln(`\x1b[1;31m${message}\x1b[0m`)
      terminal?.writeln('')
    } finally {
      setIsSubmitting(false)
      fitAddonRef.current?.fit()
      terminal?.scrollToBottom()
    }
  }

  function handleHistoryUp() {
    if (history.length === 0) {
      return
    }

    const current = historyIndexRef.current
    const next = current === null ? 0 : Math.min(current + 1, history.length - 1)

    historyIndexRef.current = next
    setCommand(history[next] ?? '')
  }

  function handleHistoryDown() {
    if (history.length === 0) {
      return
    }

    const current = historyIndexRef.current
    if (current === null) {
      return
    }

    const next = current - 1
    if (next < 0) {
      historyIndexRef.current = null
      setCommand('')
      return
    }

    historyIndexRef.current = next
    setCommand(history[next] ?? '')
  }

  return (
    <div className="flex flex-col gap-4 animate-fade-in w-full max-w-[1360px] mx-auto">
      <div className="flex items-end justify-between gap-4 flex-wrap">
        <div>
          <div className="flex items-center gap-3 mb-1">
            <div
              style={{
                width: '2px',
                height: '20px',
                background: '#6aa5da',
                borderRadius: '1px',
                boxShadow: '0 0 6px rgba(106,165,218,0.5)',
              }}
            />
            <h1
              className="font-cinzel font-semibold tracking-wider"
              style={{ color: '#f9f4ed', fontSize: '18px', letterSpacing: '0.12em' }}
            >
              Remote Shell
            </h1>
          </div>
          <p className="font-mono text-xs pl-5" style={{ color: 'rgba(185,187,211,0.45)', letterSpacing: '0.1em' }}>
            SSH-LIKE COMMAND TERMINAL
          </p>
        </div>
      </div>

      <div className="rounded-xl border border-[rgba(106,165,218,0.2)] bg-[#070b10] overflow-hidden shadow-[0_0_0_1px_rgba(106,165,218,0.08),0_16px_40px_rgba(0,0,0,0.35)]">
        <div className="h-[460px] p-3 border-b border-[rgba(106,165,218,0.16)]">
          <div ref={terminalContainerRef} className="h-full w-full" />
        </div>

        <div className="flex items-center gap-2 px-3 py-2 bg-[#0b1219]">
          <span className="font-mono text-xs text-[#6aa5da]">{PROMPT}</span>
          <input
            className="flex-1 bg-transparent font-mono text-sm text-[#e6edf3] outline-none"
            value={command}
            placeholder="type command..."
            disabled={isSubmitting}
            onChange={(event) => {
              setCommand(event.target.value)
              historyIndexRef.current = null
            }}
            onKeyDown={(event) => {
              if (event.key === 'Enter') {
                event.preventDefault()
                void executeCommand()
                return
              }

              if (event.key === 'ArrowUp') {
                event.preventDefault()
                handleHistoryUp()
                return
              }

              if (event.key === 'ArrowDown') {
                event.preventDefault()
                handleHistoryDown()
              }
            }}
          />
          <button
            type="button"
            className="font-mono text-xs uppercase tracking-wide px-2 py-1 rounded border border-[rgba(106,165,218,0.35)] text-[#6aa5da] hover:bg-[rgba(106,165,218,0.1)] disabled:opacity-50"
            onClick={() => void executeCommand()}
            disabled={isSubmitting}
          >
            {isSubmitting ? 'RUNNING' : 'ENTER'}
          </button>
        </div>
      </div>
    </div>
  )
}
