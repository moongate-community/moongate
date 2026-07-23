import { useCallback, useEffect, useRef, useState } from 'react'
import { apiFetch, apiStream } from './api'
import { readSseFrames } from './sse'

export type ConsoleStatus = 'connecting' | 'connected' | 'closed'

export interface ConsoleLine {
  id: number
  kind: 'command' | 'output'
  text: string
}

const STREAM = '/api/v1/admin/console/stream'
const SEND = '/api/v1/admin/console'

/**
 * Drives the admin web terminal: opens the authenticated SSE feed, exposes its output as a growing list
 * of lines, and sends commands to the connection the feed reported. The feed is torn down on unmount.
 */
export function useConsole() {
  const [status, setStatus] = useState<ConsoleStatus>('connecting')
  const [lines, setLines] = useState<ConsoleLine[]>([])
  const connectionId = useRef<string | null>(null)
  const nextId = useRef(0)

  const append = useCallback((kind: ConsoleLine['kind'], text: string) => {
    setLines((current) => [...current, { id: nextId.current++, kind, text }])
  }, [])

  useEffect(() => {
    const controller = new AbortController()

    async function pump() {
      try {
        const response = await apiStream(STREAM, controller.signal)
        for await (const frame of readSseFrames(response.body!, controller.signal)) {
          if (frame.event === 'ready') {
            connectionId.current = frame.data
            setStatus('connected')
          } else if (frame.event === 'line') {
            append('output', frame.data)
          }
          // 'done' marks a command's end; there is nothing to render for it in v1.
        }
      } catch {
        // Aborted on unmount, or the feed dropped — either way there is nothing left to read.
      } finally {
        if (!controller.signal.aborted) setStatus('closed')
      }
    }

    void pump()
    return () => controller.abort()
  }, [append])

  const send = useCallback(
    async (command: string) => {
      const id = connectionId.current
      if (id === null) return

      append('command', command)
      await apiFetch<void>(SEND, { method: 'POST', body: JSON.stringify({ command, connectionId: id }) })
    },
    [append],
  )

  const clear = useCallback(() => setLines([]), [])

  return { status, lines, send, clear }
}
