import { useId, useRef, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router'
import { toast } from 'sonner'
import { ErrorBanner, Field, Missing, Spinner, selectCls } from '@/components/hub/common'
import { Avatar } from '@/components/hub/people'
import { Pill, toneOf } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { ApiError, post } from '@/lib/api'
import { t, tv } from '@/lib/i18n'
import { cn } from '@/lib/utils'

export type RecordVersion = { id: string; rowVersion: number }
export type WorkRef = RecordVersion & { key: string; name: string; ownerId?: string; projectDisciplineId: string; status: string; revision?: string; transmittalUrl?: string }
export type SourceRevision = RecordVersion & { sourceKey: string; title: string; revision: string; url: string; sourceIdentity: string; sourceSystem: string; externalIdentifier: string; issuer: string; scope: string; sourceCheckedAt?: string; deliverableId?: string; supersedesId?: string; authorIds: string[]; createdAt: string }
export type SourceHead = RecordVersion & { identity: string; currentRevisionId: string; ownerId: string; projectDisciplineId: string }
export type CoordOptions = { actorId: string; canWrite: boolean; manageDisciplineIds: string[]; people: { id: string; displayName: string }[]; disciplines: { id: string; name: string }[]; sources: { revision: SourceRevision; identity: string; published: boolean; isCurrent: boolean }[]; heads: SourceHead[]; deliverables: WorkRef[]; tasks: WorkRef[] }
export type InputUse = RecordVersion & { targetType: string; targetId: string; ownerId: string; sourceIdentity: string; sourceRevisionId: string; intendedUse: string; adoptedAt: string; adoptedBy: string }
export type Choice = { value: string; label: string }
export const peopleChoices = (o: CoordOptions): Choice[] => o.people.map(p => ({ value: p.id, label: p.displayName }))
export const disciplineChoices = (o: CoordOptions): Choice[] => o.disciplines.map(p => ({ value: p.id, label: p.name }))
export const sourceChoices = (o: CoordOptions): Choice[] => o.sources.filter(s => s.published && s.isCurrent).map(s => ({ value: s.revision.id, label: `${s.revision.sourceKey} · ${s.revision.revision} · ${s.revision.title}` }))
export const workChoices = (o: CoordOptions): Choice[] => [...o.tasks.map(w => ({ value: `Task:${w.id}`, label: `${w.key} · ${w.name}` })), ...o.deliverables.map(w => ({ value: `Deliverable:${w.id}`, label: `${w.key} · ${w.name}` }))]
export const workRef = (o: CoordOptions, type: string, id: string) => (type === 'Task' ? o.tasks : o.deliverables).find(w => w.id === id)
export const personName = (o: CoordOptions, id?: string) => o.people.find(p => p.id === id)?.displayName ?? t('coord.unavailable')
export const discName = (o: CoordOptions, id: string) => o.disciplines.find(p => p.id === id)?.name ?? t('coord.unavailable')
export function SourceLink({ source }: { source: SourceRevision }) { return <a className="break-words text-primary underline underline-offset-4" href={source.url} target="_blank" rel="noopener noreferrer"><span className="key">{source.sourceKey}</span> · {source.revision} · {source.title}</a> }
export function WorkLink({ options, type, id, number }: { options: CoordOptions; type: string; id: string; number: string }) { const w = workRef(options, type, id); return w ? <Link className="break-words text-primary underline underline-offset-4" to={`/projects/${number}/${type === 'Task' ? 'tasks' : 'deliverables'}?panel=${type}:${id}`}><span className="key">{w.key}</span> · {w.name}</Link> : <span className="text-muted-foreground">{t('coord.unavailable')}</span> }
export function SelectField({ label, value, onChange, choices, required = true }: { label: string; value: string; onChange: (v: string) => void; choices: Choice[]; required?: boolean }) {
  const id = useId()
  return <Field label={label} htmlFor={id}><select id={id} className={selectCls} required={required} value={value} onChange={e => onChange(e.target.value)}><option value="">{t('coord.choose')}</option>{choices.map(c => <option key={c.value} value={c.value}>{c.label}</option>)}</select></Field>
}

/** A coordination state as a pill; a missing state reads as unavailable, never as a pass. */
export function CoordStatus({ status }: { status?: string | null }) {
  return status ? <Pill tone={toneOf(status)}>{tv(status)}</Pill> : <Missing />
}
/** A count that needs attention keeps its symbol (§13.0); an unknown count is never shown as zero. */
export function Count({ n, tone }: { n?: number | null; tone: 'bad' | 'warn' }) {
  if (n == null) return <Missing />
  if (n <= 0) return <>{n}</>
  return <span className={cn('font-semibold', tone === 'bad' ? 'text-bad' : 'text-warn')}><span aria-hidden>{tone === 'bad' ? '■ ' : '▲ '}</span>{n}</span>
}
/** A person with their stable identity mark; someone no longer listed reads as unavailable, never as a blank. */
export function PersonLabel({ id, name, options }: { id?: string | null; name?: string | null; options?: CoordOptions }) {
  const shown = name ?? options?.people.find(p => p.id === id)?.displayName
  if (!shown) return <span className="text-muted-foreground">{t('coord.unavailable')}</span>
  return <span className="inline-flex min-w-0 items-center gap-2 align-middle"><Avatar id={id} name={shown} /><span className="break-words">{shown}</span></span>
}
/** A register's pages (§13.0): a labelled navigation landmark with Previous, the page of the total, and Next. */
export function RegisterPager({ label, page, total, pageSize, onPage }: { label: string; page: number; total: number; pageSize: number; onPage: (page: number) => void }) {
  return <nav aria-label={label} className="flex flex-wrap items-center justify-end gap-3">
    <Button variant="outline" disabled={page <= 1} onClick={() => onPage(page - 1)}>{t('handoff.previous')}</Button>
    <span className="text-sm tabular-nums" aria-live="polite">{t('common.pageOf', { page, pages: Math.max(1, Math.ceil(total / Math.max(1, pageSize))) })}</span>
    <Button variant="outline" disabled={page * pageSize >= total} onClick={() => onPage(page + 1)}>{t('handoff.next')}</Button>
  </nav>
}

export function useCoordRefresh(projectId: string) {
  const qc = useQueryClient()
  return () => { for (const key of ['reviews', 'review-detail', 'changes', 'change-detail', 'coord-options', 'input-uses', 'handoffs', 'handoff-detail', 'submissions', 'submission-detail', 'search']) qc.invalidateQueries({ queryKey: key === 'search' ? [key] : [key, projectId] }); qc.invalidateQueries({ queryKey: ['p', projectId] }); qc.invalidateQueries({ queryKey: ['deliverable'] }); qc.invalidateQueries({ queryKey: ['workspace-coordination'] }) }
}
export function CommandForm({ path, title, hint, payload, children, onClose, onDone, submitLabel }: { path: string; title: string; hint?: string; payload: () => object; children?: ReactNode; onClose: () => void; onDone: (id: string) => void; submitLabel?: string }) {
  const [busy, setBusy] = useState(false), [error, setError] = useState<unknown>(null)
  const receipt = useRef<{ signature: string; id: string } | null>(null)
  async function submit(e: React.FormEvent) {
    e.preventDefault(); setError(null); setBusy(true)
    try {
      const body = payload(), signature = JSON.stringify(body)
      if (!receipt.current || receipt.current.signature !== signature) receipt.current = { signature, id: crypto.randomUUID() }
      const result = await post<{ id: string }>(path, { ...body, requestId: receipt.current.id })
      toast.success(t('coord.saved')); onDone(result.id)
    } catch (e) { setError(e) } finally { setBusy(false) }
  }
  // A refusal names each blocker exactly as the server states it, beside the banner, so the user can fix the right record.
  const refusals = error instanceof ApiError ? Object.entries(error.fieldErrors).flatMap(([field, messages]) => messages.map((message, i) => ({ key: `${field}-${i}`, message }))) : []
  return <Dialog open onOpenChange={o => !o && !busy && onClose()}><DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl"><DialogHeader><DialogTitle>{title}</DialogTitle><DialogDescription>{hint ?? t('coord.commandHint')}</DialogDescription></DialogHeader>
    <form onSubmit={submit} className="space-y-4"><fieldset disabled={busy} className="space-y-4">{children}</fieldset>{error != null && <ErrorBanner error={error} />}
      {refusals.length > 0 && <div className="rounded-md border border-bad/30 bg-bad-bg px-4 py-3 text-sm text-bad"><ul className="list-disc space-y-1 pl-5" aria-label={t('coord.refusalReasons')}>
        {refusals.map(r => <li key={r.key}>{r.message}</li>)}
      </ul></div>}
      {error instanceof ApiError && error.status === 409 && <p role="status" className="flex gap-2 rounded-md border border-warn/40 bg-warn-bg px-4 py-3 text-sm text-warn"><span aria-hidden>▲</span><span>{t('coord.stale')}</span></p>}
      <DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={onClose}>{t('common.cancel')}</Button><Button type="submit" disabled={busy}>{busy && <Spinner />}{busy ? t('common.saving') : submitLabel ?? t('coord.confirm')}</Button></DialogFooter>
    </form></DialogContent></Dialog>
}
export type FormField = { name: string; label: string; type?: 'text' | 'textarea' | 'url' | 'date' | 'datetime-local' | 'number'; choices?: Choice[]; optional?: boolean; min?: number }
export function FieldsCommand({ fields, initial = {}, build, ...props }: Omit<Parameters<typeof CommandForm>[0], 'payload' | 'children'> & { fields: FormField[]; initial?: Record<string, string>; build: (values: Record<string, string>) => object }) {
  const [v, set] = useState(initial), prefix = useId()
  return <CommandForm {...props} payload={() => build(v)}>{fields.map(f => {
    const id = `${prefix}-${f.name}`, value = v[f.name] ?? '', update = (val: string) => set(old => ({ ...old, [f.name]: val }))
    if (f.choices) return <SelectField key={f.name} label={t(f.label)} value={value} onChange={update} choices={f.choices} required={!f.optional} />
    return <Field key={f.name} label={t(f.label)} htmlFor={id} optional={f.optional}>{f.type === 'textarea' ? <Textarea id={id} value={value} required={!f.optional} maxLength={4000} onChange={e => update(e.target.value)} /> : <Input id={id} type={f.type ?? 'text'} value={value} required={!f.optional} min={f.min} step={f.type === 'number' ? 'any' : undefined} maxLength={2000} onChange={e => update(e.target.value)} />}</Field>
  })}</CommandForm>
}
