import { PersonPanelTrigger } from './person-panel-trigger'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Pencil, Trash2 } from 'lucide-react'
import { useId, useMemo, useRef, useState, type KeyboardEvent } from 'react'
import { ConfirmDialog, Empty, ErrorBanner, Loading, Spinner } from '@/components/hub/common'
import { DraftNotice } from '@/components/hub/drafts'
import { Avatar, type Person } from '@/components/hub/people'
import { RichText } from '@/components/hub/richtext'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { useSafeForm } from '@/hooks/drafts'
import { get, qs, typedRequest } from '@/lib/api'
import type { components } from '@/gen/api'
import { refreshComments } from '@/lib/keys'
import { fmtTime } from '@/lib/format'
import { t } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import type { ItemPartProps } from '@/pages/projects/slots-items'

// Comments (§12.8): conversation on the item, newest last; the facts stay in History (C-08).

type CommentRow = components['schemas']['CommentRow']
type CommentList = components['schemas']['CommentListResponse']

export function CommentsPanel({ type, id, projectId }: ItemPartProps) {
  const qc = useQueryClient()
  const q = useQuery<CommentList>({ queryKey: ['comments', type, id], queryFn: () => typedRequest('get', '/api/v1/items/{type}/{id}/comments', { params: { type, id } }) })
  const [editing, setEditing] = useState<string | null>(null)
  const [removing, setRemoving] = useState<CommentRow | null>(null)
  const reload = () => { void refreshComments(qc, projectId) }
  if (q.isPending) return <Loading rows={3} />
  if (q.error) return <div className="p-4"><ErrorBanner error={q.error} retry={() => q.refetch()} /></div>
  const items = q.data.items
  return (
    <div className="space-y-4 p-4">
      {items.length === 0 ? <Empty>{t('comments.empty')}</Empty> : (
        <ol className="space-y-4" aria-label={t('common.comments')}>
          {items.map((c) => (
            <li key={c.id} className="flex gap-3">
              <Avatar id={c.authorId} name={c.authorName} className="mt-0.5" />
              <div className="min-w-0 flex-1">
                <div className="flex min-h-8 flex-wrap items-center gap-x-2">
                  <span className="text-sm font-semibold">{c.authorId && c.authorName ? <PersonPanelTrigger userId={c.authorId} name={c.authorName} focusKey={`comment:${type}:${id}:${c.id}:author`} /> : c.authorName ?? t('common.unavailable')}</span>
                  {c.commentKind === 'Review' && <span data-accent="lavender" className="rounded-md bg-(--acc-bg) px-1.5 text-xs/[18px] font-medium text-(--acc-fg)">{t('comments.review', { n: c.reviewRound ?? 1 })}</span>}
                  {c.commentKind === 'Status Note' && <span className="rounded-md bg-secondary px-1.5 text-xs/[18px] font-medium text-muted-foreground">{t('comments.statusNote')}</span>}
                  <time className="text-xs/[18px] text-muted-foreground tabular-nums" dateTime={c.createdAt} title={c.createdAt}>{fmtTime(c.createdAt)}</time>
                  {c.editedAt && !c.deleted && <span className="text-xs/[18px] text-muted-foreground">{t('comments.edited')}</span>}
                  <span className="ml-auto flex gap-0.5">
                    {c.canEdit && editing !== c.id && <Button variant="ghost" size="icon-sm" aria-label={t('common.edit')} onClick={() => setEditing(c.id)}><Pencil className="size-4" /></Button>}
                    {c.canDelete && <Button variant="ghost" size="icon-sm" aria-label={t('common.delete')} onClick={() => setRemoving(c)}><Trash2 className="size-4" /></Button>}
                  </span>
                </div>
                {c.deleted ? (
                  <div className="text-sm italic text-muted-foreground">
                    {c.deletedByPm ? t('comments.removedByPm') : t('comments.deletedByAuthor')}
                    {c.body && <div className="mt-1 rounded-md border border-dashed p-2 not-italic" title={t('comments.adminOnly')}><RichText text={c.body} mentions={c.mentions} focusKey={`comment:${type}:${id}:${c.id}:deleted-body`} /></div>}
                  </div>
                ) : editing === c.id ? (
                  <Composer projectId={projectId} draftKey={`comment.edit.${c.id}`} initial={c.body ?? ''} submitLabel={t('common.save')} onCancel={() => setEditing(null)}
                    onSubmit={async (body) => { await typedRequest('patch', '/api/v1/comments/{id}', { params: { id: c.id }, body: { body } }); setEditing(null); reload() }} />
                ) : <RichText text={c.body} className="text-sm" mentions={c.mentions} focusKey={`comment:${type}:${id}:${c.id}:body`} />}
              </div>
            </li>
          ))}
        </ol>
      )}
      {q.data.canComment
        ? <Composer projectId={projectId} draftKey={`comment.${type}.${id}`} submitLabel={t('comments.post')} onSubmit={async (body) => { await typedRequest('post', '/api/v1/items/{type}/{id}/comments', { params: { type, id }, body: { body } }); reload() }} />
        : <p className="text-sm text-muted-foreground">{q.data.commentReason ?? t('comments.readOnly')}</p>}
      {removing && (
        <ConfirmDialog open destructive title={t('comments.deleteTitle')} body={t('comments.deleteBody')} confirmLabel={t('common.delete')}
          onOpenChange={(o) => !o && setRemoving(null)} onConfirm={async () => { await typedRequest('delete', '/api/v1/comments/{id}', { params: { id: removing.id } }); setRemoving(null); reload() }} />
      )}
    </div>
  )
}
/** Comment box: Enter adds a line, Ctrl+Enter posts; "@" opens people, project members first (C-05). */
function Composer({ projectId, draftKey, initial = '', submitLabel, onSubmit, onCancel }: {
  projectId: string; draftKey: string; initial?: string; submitLabel: string; onSubmit: (body: string) => Promise<unknown>; onCancel?: () => void
}) {
  const fieldId = useId()
  const [text, setText] = useState(initial)
  const draft = useSafeForm(draftKey, text, setText) // FR-SAV-03: a half-written comment survives a closed panel, a refresh and a dropped session
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState<unknown>(null)
  const [mention, setMention] = useState<{ query: string; start: number } | null>(null)
  const [active, setActive] = useState(0)
  const area = useRef<HTMLTextAreaElement>(null)
  const team = useQuery({ queryKey: ['p', projectId, 'team'], queryFn: () => typedRequest('get', '/api/v1/projects/{id}/team', { params: { id: projectId } }), staleTime: 60_000, enabled: !!mention })
  const others = useQuery({ queryKey: ['people', mention?.query], queryFn: () => get<Person[]>(`users${qs({ q: mention!.query, limit: 8 })}`), enabled: !!mention && mention.query.length > 0, staleTime: 60_000 })
  const options = useMemo(() => {
    if (!mention) return []
    const ql = mention.query.toLowerCase()
    const members = (team.data?.members ?? []).filter((m) => m.isActive && m.displayName.toLowerCase().includes(ql)).map((m) => ({ id: m.userId, name: m.displayName, member: true }))
    const rest = (others.data ?? []).filter((p) => !members.some((m) => m.id === p.id)).map((p) => ({ id: p.id, name: p.displayName, member: false }))
    return [...members, ...rest].slice(0, 8)
  }, [mention, team.data, others.data])

  const detect = (value: string, caret: number) => {
    const m = /(^|\s)@([\p{L}\p{N}.'-]{0,30}(?: [\p{L}\p{N}.'-]{0,30})?)$/u.exec(value.slice(0, caret))
    setMention(m ? { query: m[2], start: caret - m[2].length - 1 } : null)
    setActive(0)
  }
  const choose = (o: { id: string; name: string }) => {
    if (!mention || !area.current) return
    const caret = area.current.selectionStart
    const token = `@[${o.name}](${o.id}) `
    const next = text.slice(0, mention.start) + token + text.slice(caret)
    setText(next)
    setMention(null)
    requestAnimationFrame(() => { const pos = mention.start + token.length; area.current?.focus(); area.current?.setSelectionRange(pos, pos) })
  }
  const submit = async () => {
    if (!text.trim() || busy) return
    setBusy(true); setErr(null)
    try { await onSubmit(text.trim()); draft.clear(); setText('') } catch (e) { setErr(e) } finally { setBusy(false) }
  }
  const onKey = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (mention && options.length > 0) {
      if (e.key === 'ArrowDown') { e.preventDefault(); setActive((a) => (a + 1) % options.length); return }
      if (e.key === 'ArrowUp') { e.preventDefault(); setActive((a) => (a - 1 + options.length) % options.length); return }
      if (e.key === 'Enter' || e.key === 'Tab') { e.preventDefault(); choose(options[active]); return }
    }
    if (e.key === 'Escape') { if (mention) setMention(null); else if (onCancel) draft.close(onCancel); return }
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) { e.preventDefault(); submit() }
  }
  const listId = `${fieldId}-mentions`
  return (
    <div className="space-y-2">
      <Label htmlFor={fieldId}>{t('comments.box')}</Label>
      <DraftNotice form={draft} />
      <div className="relative">
        <Textarea id={fieldId} ref={area} rows={3} value={text} placeholder={t('comments.placeholder')} aria-describedby={`${fieldId}-hint`}
          aria-autocomplete="list" aria-controls={mention && options.length > 0 ? listId : undefined}
          onChange={(e) => { setText(e.target.value); detect(e.target.value, e.target.selectionStart) }} onKeyDown={onKey}
          onBlur={() => setTimeout(() => setMention(null), 150)} />
        {mention && options.length > 0 && (
          <ul id={listId} role="listbox" aria-label={t('comments.mentionPeople')} className="absolute left-0 top-full z-20 mt-1 w-72 max-w-full overflow-hidden rounded-md border bg-popover py-1 shadow-popover">
            {options.map((o, i) => (
              <li key={o.id} role="option" aria-selected={i === active}>
                <button type="button" onMouseDown={(e) => { e.preventDefault(); choose(o) }}
                  className={cn('flex min-h-(--control-row-h) w-full items-center gap-2 px-3 py-1 text-left text-sm', i === active ? 'bg-accent text-accent-foreground' : 'hover:bg-muted')}>
                  <Avatar id={o.id} name={o.name} className="size-6" /><span className="flex-1 truncate">{o.name}</span>
                  {!o.member && <span className="text-xs/[18px] text-muted-foreground">{t('comments.notOnTeam')}</span>}
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
      {err != null && <ErrorBanner error={err} />}
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span id={`${fieldId}-hint`} className="text-xs/[18px] text-muted-foreground">{t('comments.hint')}</span>
        <div className="flex gap-2">
          {onCancel && <Button variant="ghost" onClick={() => draft.close(onCancel)}>{t('common.cancel')}</Button>}
          <Button disabled={!text.trim() || busy} onClick={submit}>{busy && <Spinner />}{busy ? t('common.saving') : submitLabel}</Button>
        </div>
      </div>
    </div>
  )
}
