import { useState } from 'react'
import { ErrorBanner, Field } from '@/components/hub/common'
import { DraftNotice } from '@/components/hub/drafts'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { useSafeForm } from '@/hooks/drafts'
import { typedRequest } from '@/lib/api'
import { ago, fmtDate } from '@/lib/format'
import { runtimeText, t  } from '@/lib/i18n'
import type { ProjectStatusNoteData } from '@/lib/types'

type StatusNoteEdit = { id: string; rowVersion: number; needsReason: boolean; refresh: () => void }
export function ProjectStatusNote({ note, edit }: { note?: ProjectStatusNoteData | null; edit?: StatusNoteEdit }) {
  const [editing, setEditing] = useState(false)
  if (!note && !edit) return null
  return <section className="rounded-lg border bg-card p-4 text-sm" aria-label={t('statusNote.title')}>
    {note && <><p className="text-xs text-muted-foreground">{runtimeText(`health.${note.header.computedHealth}`)}{note.header.reportedHealth && ` · ${t('statusNote.reported')}: ${runtimeText(`health.${note.header.reportedHealth}`)}`}{note.header.nextMilestone && ` · ${note.header.nextMilestone.name} ${fmtDate(note.header.nextMilestone.date)}`} · {t('statusNote.attention', { count: note.header.openAttention })}</p><p className="mt-2 whitespace-pre-wrap break-words">{note.text}</p><p className="mt-1 text-xs text-muted-foreground">{t('statusNote.updated', { when: ago(note.at), name: note.author ?? '' })}</p></>}
    {edit && (editing ? <StatusNoteForm edit={edit} initial={note?.text ?? ''} onClose={() => setEditing(false)} /> : <Button variant="ghost" size="sm" onClick={() => setEditing(true)}>{t('statusNote.edit')}</Button>)}
  </section>
}

/** FR-SAV-03: the editor keeps a draft of the note while it is open and asks before Cancel throws typed text away (FR-SAV-04). */
function StatusNoteForm({ edit, initial, onClose }: { edit: StatusNoteEdit; initial: string; onClose: () => void }) {
  const [text, setText] = useState(initial)
  const [reason, setReason] = useState('')
  const [error, setError] = useState<unknown>(null)
  const [busy, setBusy] = useState(false)
  const safe = useSafeForm(`status-note.${edit.id}`, { text, reason }, (d) => { setText(d.text); setReason(d.reason) })
  const save = async () => { setBusy(true); setError(null); try { await typedRequest('patch', '/api/v1/projects/{id}', { params: { id: edit.id }, body: { statusNote: text || null, reason }, ifMatch: edit.rowVersion }); safe.clear(); onClose(); edit.refresh() } catch (e) { setError(e) } finally { setBusy(false) } }
  return <>
    {error != null && <ErrorBanner error={error} />}
    <form className="mt-3 space-y-3" onSubmit={e => { e.preventDefault(); void save() }}><DraftNotice form={safe} /><Field label={t('statusNote.title')} htmlFor="project-status-note"><Textarea id="project-status-note" maxLength={500} value={text} disabled={busy} onChange={e => setText(e.target.value)} /></Field>{edit.needsReason && <Field label={t('common.reason')} htmlFor="status-note-reason"><Textarea id="status-note-reason" value={reason} onChange={e => setReason(e.target.value)} required minLength={5} disabled={busy} /></Field>}<Button type="submit" disabled={busy || edit.needsReason && reason.trim().length < 5}>{t('common.save')}</Button> <Button type="button" variant="ghost" onClick={() => safe.close(onClose)} disabled={busy}>{t('common.cancel')}</Button></form>
  </>
}
