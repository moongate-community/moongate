import { act, renderHook, waitFor } from '@testing-library/react'
import { useConsole } from './console'

// A stream that emits the given frames and then stays open, so the hook settles on 'connected' the way a
// live console feed does (rather than ending and flipping to 'closed').
function openStream(...frames: string[]): ReadableStream<Uint8Array> {
  const encoder = new TextEncoder()
  return new ReadableStream({
    start(controller) {
      for (const frame of frames) controller.enqueue(encoder.encode(frame))
    },
  })
}

function serve(...frames: string[]) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const url = String(input)
    if (url.endsWith('/api/v1/admin/console/stream')) {
      return new Response(openStream(...frames), { status: 200, headers: { 'content-type': 'text/event-stream' } })
    }
    if (url.endsWith('/api/v1/admin/console') && (init as RequestInit)?.method === 'POST') {
      return new Response(null, { status: 202 })
    }
    return new Response('{}', { status: 200 })
  })
}

describe('useConsole', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('connects and appends output lines from the stream', async () => {
    serve('event: ready\ndata: conn-1\n\n', 'event: line\ndata: 2 players online\n\n')
    const { result } = renderHook(() => useConsole())

    await waitFor(() => expect(result.current.status).toBe('connected'))
    await waitFor(() =>
      expect(result.current.lines.some((line) => line.kind === 'output' && line.text === '2 players online')).toBe(
        true,
      ),
    )
  })

  it('echoes the command and POSTs it with the connection id', async () => {
    const fetchSpy = serve('event: ready\ndata: conn-1\n\n')
    const { result } = renderHook(() => useConsole())
    await waitFor(() => expect(result.current.status).toBe('connected'))

    await act(() => result.current.send('who'))

    const post = fetchSpy.mock.calls.find(([, init]) => (init as RequestInit)?.method === 'POST')!
    expect(JSON.parse((post[1] as RequestInit).body as string)).toEqual({ command: 'who', connectionId: 'conn-1' })
    expect(result.current.lines.some((line) => line.kind === 'command' && line.text === 'who')).toBe(true)
  })

  it('clear empties the lines', async () => {
    serve('event: ready\ndata: conn-1\n\n', 'event: line\ndata: hello\n\n')
    const { result } = renderHook(() => useConsole())
    await waitFor(() => expect(result.current.lines.length).toBeGreaterThan(0))

    act(() => result.current.clear())
    expect(result.current.lines).toHaveLength(0)
  })
})
