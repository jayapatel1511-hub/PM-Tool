import { useId, useRef, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router'
import { toast } from 'sonner'
import { ErrorBanner, Field, Spinner, selectCls } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { ApiError, post } from '@/lib/api'
import { t } from '@/lib/i18n'

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
export function SourceLink({ source }: { source: SourceRevision }) { return <a className="text-primary underline" href={source.url} target="_blank" rel="noopener noreferrer">{source.sourceKey} · {source.revision} · {source.title}</a> }
export function WorkLink({ options, type, id, number }: { options: CoordOptions; type: string; id: string; number: string }) { const w = workRef(options, type, id); return w ? <Link className="text-primary underline" to={`/projects/${number}/${type === 'Task' ? 'tasks' : 'deliverables'}?panel=${type}:${id}`}>{w.key} · {w.name}</Link> : <span>{t('coord.unavailable')}</span> }
export function SelectField({ label, value, onChange, choices, required = true }: { label: string; value: string; onChange: (v: string) => void; choices: Choice[]; required?: boolean }) {
  const id = useId()
  return <Field label={label} htmlFor={id}><select id={id} className={selectCls} required={required} value={value} onChange={e => onChange(e.target.value)}><option value="">{t('coord.choose')}</option>{choices.map(c => <option key={c.value} value={c.value}>{c.label}</option>)}</select></Field>
}
export function useCoordRefresh(projectId: string) {
  const qc = useQueryClient()
  return () => { for (const key of ['reviews', 'review-detail', 'changes', 'change-detail', 'coord-options', 'input-uses', 'handoffs', 'handoff-detail', 'search']) qc.invalidateQueries({ queryKey: key === 'search' ? [key] : [key, projectId] }); qc.invalidateQueries({ queryKey: ['p', projectId] }); qc.invalidateQueries({ queryKey: ['deliverable'] }) }
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
  return <Dialog open onOpenChange={o => !o && !busy && onClose()}><DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl"><DialogHeader><DialogTitle>{title}</DialogTitle><DialogDescription>{hint ?? t('coord.commandHint')}</DialogDescription></DialogHeader>
    <form onSubmit={submit} className="space-y-4"><fieldset disabled={busy} className="space-y-4">{children}</fieldset>{error != null && <ErrorBanner error={error} />}{error instanceof ApiError && error.status === 409 && <p role="status" className="text-sm text-warn">{t('coord.stale')}</p>}
      <DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={onClose}>{t('common.cancel')}</Button><Button type="submit" disabled={busy}>{busy && <Spinner />}{submitLabel ?? t('coord.confirm')}</Button></DialogFooter>
    </form></DialogContent></Dialog>
}
export type FormField = { name: string; label: string; type?: 'text' | 'textarea' | 'url' | 'date' | 'datetime-local' | 'number'; choices?: Choice[]; optional?: boolean; min?: number }
export function FieldsCommand({ fields, initial = {}, build, ...props }: Omit<Parameters<typeof CommandForm>[0], 'payload' | 'children'> & { fields: FormField[]; initial?: Record<string, string>; build: (values: Record<string, string>) => object }) {
  const [v, set] = useState(initial), prefix = useId()
  return <CommandForm {...props} payload={() => build(v)}>{fields.map(f => {
    const id = `${prefix}-${f.name}`, value = v[f.name] ?? '', update = (val: string) => set(old => ({ ...old, [f.name]: val }))
    if (f.choices) return <SelectField key={f.name} label={t(f.label)} value={value} onChange={update} choices={f.choices} required={!f.optional} />
    return <Field key={f.name} label={t(f.label)} htmlFor={id}>{f.type === 'textarea' ? <Textarea id={id} value={value} required={!f.optional} maxLength={4000} onChange={e => update(e.target.value)} /> : <Input id={id} type={f.type ?? 'text'} value={value} required={!f.optional} min={f.min} step={f.type === 'number' ? 'any' : undefined} maxLength={2000} onChange={e => update(e.target.value)} />}</Field>
  })}</CommandForm>
}
