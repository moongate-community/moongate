import { useEffect, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'

import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '../../components/ui/dialog'
import { Input } from '../../components/ui/input'
import { Label } from '../../components/ui/label'
import { Button } from '../../components/ui/button'
import { Switch } from '../../components/ui/switch'
import { toast } from '../../components/ui/sonner'
import { useCreateNews, useUpdateNews, type News } from '../../lib/news'

/**
 * Writes a news entry, new or existing. One dialog for both: the fields are identical, and two would
 * be two places to keep the publish warning in step.
 */
export function NewsDialog({
  entry,
  open,
  onOpenChange,
}: {
  entry: News | null
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const { t } = useTranslation()
  const create = useCreateNews()
  const update = useUpdateNews()
  const [title, setTitle] = useState('')
  const [body, setBody] = useState('')
  const [isPublished, setIsPublished] = useState(false)

  // The dialog outlives the entry it edits, so its fields are refilled whenever the subject changes.
  useEffect(() => {
    setTitle(entry?.title ?? '')
    setBody(entry?.body ?? '')
    setIsPublished(entry?.isPublished ?? false)
  }, [entry, open])

  async function submit(event: FormEvent) {
    event.preventDefault()

    try {
      if (entry === null) {
        await create.mutateAsync({ title, body, isPublished })
      } else {
        await update.mutateAsync({ id: entry.id, patch: { title, body, isPublished } })
      }

      onOpenChange(false)
    } catch {
      toast.error(t('error.generic'))
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <form onSubmit={submit} className="flex flex-col gap-4">
          <DialogHeader>
            <DialogTitle>{entry === null ? t('admin.news.new') : t('admin.news.edit')}</DialogTitle>
          </DialogHeader>

          <div className="flex flex-col gap-2">
            <Label htmlFor="news-title">{t('admin.news.newsTitle')}</Label>
            <Input id="news-title" value={title} onChange={(e) => setTitle(e.target.value)} required />
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="news-body">{t('admin.news.body')}</Label>
            <textarea
              id="news-body"
              value={body}
              onChange={(e) => setBody(e.target.value)}
              rows={6}
              className="rounded-md border bg-transparent px-3 py-2 text-sm text-ink outline-none focus-visible:ring-2 focus-visible:ring-gold"
            />
          </div>

          <div className="flex items-center gap-3">
            <Switch id="news-published" checked={isPublished} onCheckedChange={setIsPublished} />
            <Label htmlFor="news-published">{t('admin.news.publish')}</Label>
          </div>

          {/* Publishing interrupts everyone in the world, so it should not be a surprise. */}
          {isPublished && !(entry?.isPublished ?? false) && (
            <p className="text-xs text-muted">{t('admin.news.announceHint')}</p>
          )}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              {t('admin.news.cancel')}
            </Button>
            <Button type="submit">{t('admin.news.save')}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
