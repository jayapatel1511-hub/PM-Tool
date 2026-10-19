import { useState } from 'react'
import { ErrorBanner, Field } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { patch } from '@/lib/api'
import { ago, fmtDate } from '@/lib/format'
import { runtimeText, t  } from '@/lib/i18n'
import type { ProjectStatusNoteData } from '@/lib/types'

export function ProjectStatusNote({ note, edit }: { note?: ProjectStatusNoteData | null; edit?: { id: string; rowVersion: number; needsReason: boolean; refresh: () => void } }) {
  const [editing, setEditing] = useState(false)
  const [text, setText] = useState(note?.text ?? '')
  const [reason, setReason] = useState('')
  const [error, setError] = useState<unknown>(null)
  const [busy, setBusy] = useState(false)
  const save = async () => { if (!edit) return; setBusy(true); setError(null); try { await patch(`projects/${edit.id}`, { statusNote: text || null, reason }, edit.rowVersion); setEditing(false); edit.refresh() } catch (e) { setError(e) } finally { setBusy(false) } }
  if (!note && !edit) return null
  return <section className="rounded-lg border bg-card p-4 text-sm" aria-label={t('statusNote.title')}>
    {note && <><p className="text-xs text-muted-foreground">{runtimeText(`health.${note.header.computedHealth}`)}{note.header.reportedHealth && ` · ${t('statusNote.reported')}: ${runtimeText(`health.${note.header.reportedHealth}`)}`}{note.header.nextMilestone && ` · ${note.header.nextMilestone.name} ${fmtDate(note.header.nextMilestone.date)}`} · {t('statusNote.attention', { count: note.header.openAttention })}</p><p className="mt-2 whitespace-pre-wrap break-words">{note.text}</p><p className="mt-1 text-xs text-muted-foreground">{t('statusNote.updated', { when: ago(note.at), name: note.author ?? '' })}</p></>}
    {error != null && <ErrorBanner error={error} />}
    {edit && (editing ? <form className="mt-3 space-y-3" onSubmit={e => { e.preventDefault(); void save() }}><Field label={t('statusNote.title')} htmlFor="project-status-note"><Textarea id="project-status-note" maxLength={500} value={text} disabled={busy} onChange={e => setText(e.target.value)} /></Field>{edit.needsReason && <Field label={t('common.reason')} htmlFor="status-note-reason"><Textarea id="status-note-reason" value={reason} onChange={e => setReason(e.target.value)} required minLength={5} disabled={busy} /></Field>}<Button type="submit" disabled={busy || edit.needsReason && reason.trim().length < 5}>{t('common.save')}</Button> <Button type="button" variant="ghost" onClick={() => setEditing(false)} disabled={busy}>{t('common.cancel')}</Button></form> : <Button variant="ghost" size="sm" onClick={() => { setText(note?.text ?? ''); setEditing(true) }}>{t('statusNote.edit')}</Button>)}
  </section>
}
