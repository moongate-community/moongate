import { useEffect, useRef, useState, type KeyboardEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Card } from '../components/ui/card'
import { Button } from '../components/ui/button'
import { Badge } from '../components/ui/badge'
import { useSession } from '../lib/auth'
import { useConsole, type ConsoleStatus } from '../lib/console'

const PROMPT = 'moongate>'

// The commands the REST console actually accepts today; clicking one pre-fills the input.
const SNIPPETS = ['broadcast <message>', 'bc <message>']

const DOT: Record<ConsoleStatus, string> = {
  connected: 'bg-success shadow-[0_0_6px_rgba(111,201,141,0.7)]',
  connecting: 'bg-gold',
  closed: 'bg-danger-text',
}

const STATUS_BADGE: Record<ConsoleStatus, 'success' | 'warning' | 'danger'> = {
  connected: 'success',
  connecting: 'warning',
  closed: 'danger',
}

export function ConsoleScreen() {
  const { t } = useTranslation()
  const { username } = useSession()
  const { status, lines, send, clear } = useConsole()
  const [input, setInput] = useState('')

  // Submitted commands, newest last, plus a cursor into them for ↑/↓ recall (-1 = editing a fresh line).
  const history = useRef<string[]>([])
  const cursor = useRef(-1)
  const output = useRef<HTMLDivElement>(null)
  const field = useRef<HTMLInputElement>(null)

  // Keep the newest line in view as output streams in.
  useEffect(() => {
    output.current?.scrollTo({ top: output.current.scrollHeight })
  }, [lines])

  function submit() {
    const command = input.trim()
    if (command === '') {
      return
    }
    history.current = [...history.current, command]
    cursor.current = -1
    setInput('')
    void send(command)
  }

  function onKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    const past = history.current

    if (event.key === 'Enter') {
      event.preventDefault()
      submit()
    } else if (event.key === 'ArrowUp') {
      event.preventDefault()
      if (past.length === 0) {
        return
      }
      cursor.current = cursor.current === -1 ? past.length - 1 : Math.max(0, cursor.current - 1)
      setInput(past[cursor.current])
    } else if (event.key === 'ArrowDown') {
      event.preventDefault()
      if (cursor.current === -1) {
        return
      }
      const next = cursor.current + 1
      if (next >= past.length) {
        cursor.current = -1
        setInput('')
      } else {
        cursor.current = next
        setInput(past[next])
      }
    }
  }

  function useSnippet(snippet: string) {
    setInput(snippet)
    field.current?.focus()
  }

  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-xl text-ink">{t('admin.console.title')}</h1>

      <div className="flex items-center gap-3">
        <Badge variant="danger">{t('admin.console.live')}</Badge>
        <span className="text-sm text-muted">{t('admin.console.warning')}</span>
      </div>

      <div className="overflow-hidden rounded-control border border-border-subtle bg-deep font-mono">
        <div className="flex items-center gap-2 border-b border-border-subtle bg-surface px-3.5 py-2.5 text-[11.5px]">
          <span className={`size-[7px] rounded-full ${DOT[status]}`} aria-hidden />
          <span className="text-muted">{t('admin.console.session', { user: username ?? '' })}</span>
          <span className="ml-auto flex items-center gap-2.5">
            <Badge variant={STATUS_BADGE[status]}>{t(`admin.console.${status}`)}</Badge>
            <button type="button" onClick={clear} className="text-xs text-muted hover:text-gold">
              {t('admin.console.clear')}
            </button>
          </span>
        </div>

        <div ref={output} className="h-[360px] overflow-y-auto px-[18px] py-4 text-[13px] leading-[1.9]">
          <div className="text-faint">{t('admin.console.banner')}</div>
          {lines.map((line) =>
            line.kind === 'command' ? (
              <div key={line.id} className="whitespace-pre-wrap">
                <span className="text-gold">{PROMPT} </span>
                <span className="text-ink">{line.text}</span>
              </div>
            ) : (
              <div key={line.id} className="whitespace-pre-wrap text-ink">
                {line.text}
              </div>
            ),
          )}
        </div>

        <div className="flex items-center gap-2.5 border-t border-border-subtle px-3.5 py-2.5">
          <span className="text-[13px] text-gold" aria-hidden>
            {PROMPT}
          </span>
          <input
            ref={field}
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={onKeyDown}
            disabled={status !== 'connected'}
            placeholder={t('admin.console.placeholder')}
            autoComplete="off"
            spellCheck={false}
            className="flex-1 bg-transparent text-[13px] text-ink outline-none placeholder:text-faint disabled:opacity-50"
          />
          <span className="hidden text-xs text-faint sm:inline">{t('admin.console.hint')}</span>
          <Button type="button" onClick={submit} disabled={status !== 'connected'} className="px-4 py-2 text-[11px]">
            {t('admin.console.execute')}
          </Button>
        </div>
      </div>

      <Card className="flex flex-col gap-3 px-[18px] py-4">
        <h2 className="font-display text-[13px] tracking-[0.08em] text-gold">{t('admin.console.snippets')}</h2>
        <div className="flex flex-wrap gap-2.5 font-mono text-[12.5px]">
          {SNIPPETS.map((snippet) => (
            <button
              key={snippet}
              type="button"
              onClick={() => useSnippet(snippet)}
              className="rounded-control border border-border-subtle bg-page px-2.5 py-1.5 text-gold hover:border-gold"
            >
              {`› ${snippet}`}
            </button>
          ))}
        </div>
      </Card>
    </div>
  )
}
