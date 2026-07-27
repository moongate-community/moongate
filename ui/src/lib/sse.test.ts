import { parseSseFrame, readSseFrames } from './sse'

function streamOf(...chunks: string[]): ReadableStream<Uint8Array> {
  const encoder = new TextEncoder()
  return new ReadableStream({
    start(controller) {
      for (const chunk of chunks) controller.enqueue(encoder.encode(chunk))
      controller.close()
    },
  })
}

async function collect(stream: ReadableStream<Uint8Array>) {
  const out: { event: string; data: string }[] = []
  for await (const frame of readSseFrames(stream, new AbortController().signal)) out.push(frame)
  return out
}

describe('parseSseFrame', () => {
  it('reads the event type and data', () => {
    expect(parseSseFrame('event: line\ndata: hello')).toEqual({ event: 'line', data: 'hello' })
  })

  it('joins multiple data lines with a newline', () => {
    expect(parseSseFrame('event: line\ndata: a\ndata: b')).toEqual({ event: 'line', data: 'a\nb' })
  })

  it('defaults the event to "message" when none is given', () => {
    expect(parseSseFrame('data: x')).toEqual({ event: 'message', data: 'x' })
  })
})

describe('readSseFrames', () => {
  it('yields one object per blank-line-separated frame', async () => {
    const frames = await collect(streamOf('event: ready\ndata: conn-1\n\nevent: line\ndata: hi\n\n'))
    expect(frames).toEqual([
      { event: 'ready', data: 'conn-1' },
      { event: 'line', data: 'hi' },
    ])
  })

  it('reassembles a frame split across chunks', async () => {
    const frames = await collect(streamOf('event: li', 'ne\ndata: split', '-line\n\n'))
    expect(frames).toEqual([{ event: 'line', data: 'split-line' }])
  })
})
