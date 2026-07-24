// A minimal Server-Sent-Events reader. The browser's EventSource can't carry an Authorization header,
// so the console reads its stream over an authenticated fetch and parses the frames here instead.

export interface SseFrame {
  event: string
  data: string
}

/** Parses one SSE frame (the text between blank lines) into its event type and joined data. */
export function parseSseFrame(frame: string): SseFrame {
  let event = 'message'
  const data: string[] = []

  for (const line of frame.split('\n')) {
    if (line.startsWith('event:')) {
      event = line.slice('event:'.length).trim()
    } else if (line.startsWith('data:')) {
      // A single leading space after the colon is part of the SSE framing, not the payload.
      data.push(line.slice('data:'.length).replace(/^ /, ''))
    }
  }

  return { event, data: data.join('\n') }
}

/** Reads an SSE byte stream and yields one frame per blank-line-separated block until it ends or aborts. */
export async function* readSseFrames(body: ReadableStream<Uint8Array>, signal: AbortSignal): AsyncGenerator<SseFrame> {
  const reader = body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''

  // A pending read() does not observe the signal on its own; cancelling the reader unblocks it so the
  // loop can exit promptly when the caller aborts (e.g. the console page unmounts).
  const onAbort = () => reader.cancel().catch(() => {})
  signal.addEventListener('abort', onAbort)

  try {
    while (!signal.aborted) {
      const { value, done } = await reader.read()
      if (done) break

      buffer += decoder.decode(value, { stream: true })

      let boundary = buffer.indexOf('\n\n')
      while (boundary !== -1) {
        const frame = buffer.slice(0, boundary)
        buffer = buffer.slice(boundary + 2)
        if (frame.trim() !== '') yield parseSseFrame(frame)
        boundary = buffer.indexOf('\n\n')
      }
    }
  } finally {
    signal.removeEventListener('abort', onAbort)
    reader.cancel().catch(() => {})
  }
}
