import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowRightLeft, Plus } from 'lucide-react'
import { useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { ErrorBanner, Field, Loading, Page, Spinner, selectCls } from '@/components/hub/common'
import { ExportMenu } from '@/components/hub/export'
import { ViewMenu } from '@/components/hub/views'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { ApiError, get, post, qs } from '@/lib/api'
import { addDays, fmtDate, today } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { useCurrentProject } from './ProjectLayout'

const STATUSES = ['Draft', 'Submitted', 'Clarification Requested', 'Returned', 'Accepted', 'Incorporated', 'Cancelled']
type Person = { id: string; displayName: string }
type Work = { id: string; key: string; name: string; projectDisciplineId: string; ownerId?: string; dueDate?: string }
type Source = Work & { revision?: string; transmittalUrl?: string; rowVersion: number }
type Options = { sources: Source[]; tasks: Work[]; people: Person[]; disciplines: { id: string; name: string }[]; canCreate: boolean }
type Row = {
  id: string; key: string; title: string; status: string; rowVersion: number; sourceDeliverableId: string; sourceRowVersion: number
  declaredRevision: string; sourceUrl: string; sendingDisciplineId: string; receivingDisciplineId: string
  sendingOwnerId: string; receivingOwnerId: string; targetTaskId?: string; targetDeliverableId?: string
  intendedUse: string; acceptanceCriteria: string; neededBy: string; promisedBy?: string; currentRevisionId?: string; incorporatedRevisionId?: string
  senderName: string; receiverName: string; sourceKey: string; targetKey: string; ownersAvailable: boolean; sourceChanged: boolean; dateMismatch: boolean; isOverdue: boolean
}
type Permit = { ok: boolean; reason?: string }
type Detail = {
  changeAssessments?: { id: string; status: string; changeNoticeId: string; key: string; title: string; noticeStatus: string }[]
  row: Row; permissions: { edit: Permit; assign: Permit; transitions: { to: string; permission: Permit }[] }
  history: { id: string; fromStatus: string; toStatus: string; reason?: string; criteriaOutcome?: string; actor: string; createdAt: string; revisionId?: string }[]
  revisions: { id: string; intendedUse: string; acceptanceCriteria: string; createdAt: string; response?: string; source: { revision: string; url: string; sourceKey: string; createdAt: string } }[]
}
type Receipt = { signature: string; id: string }
// Retain the command ID on network retry; changing any input creates a new command.
function requestId(ref: { current: Receipt | null }, payload: unknown) {
  const signature = JSON.stringify(payload)
  if (!ref.current || ref.current.signature !== signature) ref.current = { signature, id: crypto.randomUUID() }
  return ref.current.id
}

export function HandoffsTab() {
  const p = useCurrentProject()
  const [sp, setSp] = useSearchParams()
  const qc = useQueryClient()
  const [create, setCreate] = useState(sp.has('source') || sp.has('targetTask'))
  const filters = Object.fromEntries(['q', 'status', 'direction', 'disciplineId', 'overdue'].map(k => [k, sp.get(k) ?? '']))
  const page = Math.max(1, Number(sp.get('page')) || 1)
  const list = useQuery({ queryKey: ['handoffs', p.id, filters, page], queryFn: () => get<{ items: Row[]; totalCount: number; pageSize: number }>(`projects/${p.id}/handoffs${qs({ ...filters, page })}`) })
  const options = useQuery({ queryKey: ['handoff-options', p.id], queryFn: () => get<Options>(`projects/${p.id}/handoffs/options`) })
  const panel = sp.get('panel')?.startsWith('Handoff:') ? sp.get('panel')!.slice(8) : null
  const setFilter = (key: string, value: string) => { const next = new URLSearchParams(sp); if (value) next.set(key, value); else next.delete(key); next.delete('page'); setSp(next) }
  const refresh = () => { qc.invalidateQueries({ queryKey: ['handoffs', p.id] }); qc.invalidateQueries({ queryKey: ['handoff-detail', p.id] }); qc.invalidateQueries({ queryKey: ['handoff-options', p.id] }); qc.invalidateQueries({ queryKey: ['search'] }) }
  const open = (id: string) => { const next = new URLSearchParams(sp); next.set('panel', `Handoff:${id}`); setSp(next) }
  const close = () => { const next = new URLSearchParams(sp); next.delete('panel'); setSp(next) }
  return <Page title={t('handoff.title')} subtitle={t('handoff.subtitle')} actions={<>
    <ViewMenu listType="handoffs" projectId={p.id} />
    <ExportMenu path={`projects/${p.id}/handoffs/export`} params={filters} name={`${p.projectNumber}-handoffs`} />
    {options.data?.canCreate && <Button size="sm" onClick={() => setCreate(true)}><Plus className="size-4" />{t('handoff.new')}</Button>}
  </>}>
    <div className="flex flex-wrap items-end gap-3 rounded-lg border bg-card p-3">
      <Field label={t('common.search')} htmlFor="handoff-search"><Input id="handoff-search" type="search" value={filters.q} onChange={e => setFilter('q', e.target.value)} /></Field>
      <Field label={t('handoff.direction')} htmlFor="handoff-direction"><select id="handoff-direction" className={selectCls} value={filters.direction} onChange={e => setFilter('direction', e.target.value)}>
        <option value="">{t('handoff.all')}</option><option value="incoming">{t('handoff.incoming')}</option><option value="outgoing">{t('handoff.outgoing')}</option>
      </select></Field>
      <Field label={t('common.status')} htmlFor="handoff-status"><select id="handoff-status" className={selectCls} value={filters.status} onChange={e => setFilter('status', e.target.value)}>
        <option value="">{t('handoff.all')}</option>{STATUSES.map(s => <option key={s} value={s}>{tv(s)}</option>)}
      </select></Field>
      <Field label={t('handoff.discipline')} htmlFor="handoff-discipline"><select id="handoff-discipline" className={selectCls} value={filters.disciplineId} onChange={e => setFilter('disciplineId', e.target.value)}>
        <option value="">{t('handoff.all')}</option>{options.data?.disciplines.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
      </select></Field>
      <label className="flex items-center gap-2 py-2 text-sm"><input type="checkbox" checked={filters.overdue === 'true'} onChange={e => setFilter('overdue', e.target.checked ? 'true' : '')} />{t('handoff.overdueOnly')}</label>
    </div>
    {options.error && <ErrorBanner error={options.error} retry={() => options.refetch()} />}
    {list.isPending ? <Loading rows={5} /> : list.error ? <ErrorBanner error={list.error} retry={() => list.refetch()} /> : <>
      <p role="status" className="text-sm text-muted-foreground">{t('handoff.count', { n: list.data.totalCount })}</p>
      {!list.data.items.length ? <div className="rounded-lg border bg-card p-10 text-center"><ArrowRightLeft className="mx-auto mb-3 size-7 text-muted-foreground" aria-hidden /><h2 className="font-medium">{t('handoff.empty')}</h2><p className="mt-1 text-sm text-muted-foreground">{t('handoff.emptyHint')}</p></div> :
        <div className="overflow-x-auto rounded-lg border bg-card"><table className="w-full text-left text-sm"><caption className="sr-only">{t('handoff.title')}</caption>
          <thead className="bg-muted/60"><tr>{['item', 'owners', 'revision', 'needed', 'promised', 'state'].map(k => <th key={k} scope="col" className="px-3 py-3 font-medium">{t(`handoff.${k}`)}</th>)}</tr></thead>
          <tbody>{list.data.items.map(h => <tr key={h.id} className="border-t align-top hover:bg-muted/30">
            <td className="min-w-56 px-3 py-3"><button className="text-left font-medium text-primary underline-offset-4 hover:underline" onClick={() => open(h.id)}>{h.key} · {h.title}</button><p className="mt-1 text-xs text-muted-foreground">{h.sourceKey} → {h.targetKey}</p></td>
            <td className="px-3 py-3"><div>{t('handoff.from', { name: h.senderName })}</div><div>{t('handoff.to', { name: h.receiverName })}</div>{!h.ownersAvailable && <p className="mt-1 text-xs text-bad">{t('handoff.ownerUnavailable')}</p>}</td>
            <td className="px-3 py-3">{h.declaredRevision}{h.sourceChanged && <p className="mt-1 max-w-44 text-xs text-warn">{t('handoff.sourceChanged')}</p>}</td>
            <td className="whitespace-nowrap px-3 py-3">{fmtDate(h.neededBy)}{h.isOverdue && <p className="text-xs text-bad">{t('handoff.overdue')}</p>}</td>
            <td className="whitespace-nowrap px-3 py-3">{fmtDate(h.promisedBy)}{h.dateMismatch && <p className="text-xs text-warn">{t('handoff.dateMismatch')}</p>}</td>
            <td className="px-3 py-3"><span className="inline-block rounded border bg-muted px-2 py-1 text-xs">{tv(h.status)}</span></td>
          </tr>)}</tbody></table></div>}
      <div className="flex items-center justify-end gap-3"><Button variant="outline" size="sm" disabled={page <= 1} onClick={() => { const next = new URLSearchParams(sp); next.set('page', String(page - 1)); setSp(next) }}>{t('handoff.previous')}</Button><span className="text-sm">{t('handoff.page', { n: page })}</span><Button variant="outline" size="sm" disabled={page * list.data.pageSize >= list.data.totalCount} onClick={() => { const next = new URLSearchParams(sp); next.set('page', String(page + 1)); setSp(next) }}>{t('handoff.next')}</Button></div>
    </>}
    {create && options.data && <HandoffForm projectId={p.id} options={options.data} initialSource={sp.get('source')} initialTask={sp.get('targetTask')} onClose={(id) => {
      setCreate(false); refresh(); const next = new URLSearchParams(sp); next.delete('source'); next.delete('targetTask'); if (id) next.set('panel', `Handoff:${id}`); setSp(next)
    }} />}
    {panel && !create && <HandoffDetail projectId={p.id} projectNumber={p.projectNumber} id={panel} options={options.data} onClose={close} refresh={refresh} />}
  </Page>
}

function PersonSelect({ id, value, onChange, people }: { id: string; value: string; onChange: (value: string) => void; people: Person[] }) {
  return <select id={id} className={selectCls} required value={value} onChange={e => onChange(e.target.value)}><option value="">{t('handoff.choose')}</option>{people.map(p => <option key={p.id} value={p.id}>{p.displayName}</option>)}</select>
}

function HandoffForm({ projectId, options, existing, initialSource, initialTask, onClose }: {
  projectId: string; options: Options; existing?: Row; initialSource?: string | null; initialTask?: string | null; onClose: (id?: string) => void
}) {
  const source = options.sources.find(s => s.id === (existing?.sourceDeliverableId ?? initialSource))
  const target = options.tasks.find(s => s.id === (existing?.targetTaskId ?? initialTask))
  const [values, set] = useState({ title: existing?.title ?? source?.name ?? '', sourceDeliverableId: source?.id ?? '',
    declaredRevision: existing?.declaredRevision ?? source?.revision ?? '', sourceUrl: existing?.sourceUrl ?? source?.transmittalUrl ?? '',
    receivingDisciplineId: existing?.receivingDisciplineId ?? target?.projectDisciplineId ?? '', sendingOwnerId: existing?.sendingOwnerId ?? source?.ownerId ?? '',
    receivingOwnerId: existing?.receivingOwnerId ?? target?.ownerId ?? '', target: existing?.targetTaskId ? `Task:${existing.targetTaskId}` : existing?.targetDeliverableId ? `Deliverable:${existing.targetDeliverableId}` : target ? `Task:${target.id}` : '',
    intendedUse: existing?.intendedUse ?? '', acceptanceCriteria: existing?.acceptanceCriteria ?? '', neededBy: existing?.neededBy ?? target?.dueDate ?? addDays(today(), 7), promisedBy: existing?.promisedBy ?? '', reason: '' })
  const update = (key: keyof typeof values, value: string) => set(v => ({ ...v, [key]: value }))
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<ApiError | null>(null)
  const receipt = useRef<Receipt | null>(null)
  const fixed = !!existing?.currentRevisionId
  const selected = options.sources.find(s => s.id === values.sourceDeliverableId)
  const save = async () => {
    setBusy(true); setError(null)
    try {
      const payload = { ...values, sourceRowVersion: selected?.rowVersion ?? existing?.sourceRowVersion,
        targetTaskId: values.target.startsWith('Task:') ? values.target.slice(5) : null, targetDeliverableId: values.target.startsWith('Deliverable:') ? values.target.slice(12) : null,
        promisedBy: values.promisedBy || null, rowVersion: existing?.rowVersion }
      const result = await post<{ id: string }>(`projects/${projectId}/handoffs${existing ? `/${existing.id}/draft` : ''}`, { ...payload, requestId: requestId(receipt, payload) })
      toast.success(t('handoff.saved')); onClose(result.id)
    } catch (e) { setError(e as ApiError) } finally { setBusy(false) }
  }
  const workOptions = (kind: string, work: Work[]) => work.filter(w => w.projectDisciplineId === values.receivingDisciplineId).map(w => <option key={w.id} value={`${kind}:${w.id}`}>{w.key} · {w.name}</option>)
  return <Dialog open onOpenChange={o => !o && !busy && onClose()}><DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-2xl">
    <DialogHeader><DialogTitle>{t(existing ? 'handoff.edit' : 'handoff.new')}</DialogTitle><DialogDescription>{t('handoff.formHint')}</DialogDescription></DialogHeader>
    <form onSubmit={e => { e.preventDefault(); save() }} className="grid gap-3 sm:grid-cols-2">
      <Field label={t('handoff.item')} htmlFor="hf-title" className="sm:col-span-2"><Input id="hf-title" required maxLength={200} value={values.title} onChange={e => update('title', e.target.value)} /></Field>
      <Field label={t('handoff.source')} htmlFor="hf-source" className="sm:col-span-2"><select id="hf-source" className={selectCls} required disabled={!!existing} value={values.sourceDeliverableId} onChange={e => {
        const s = options.sources.find(x => x.id === e.target.value); set(v => ({ ...v, sourceDeliverableId: e.target.value, declaredRevision: s?.revision ?? '', sourceUrl: s?.transmittalUrl ?? '', sendingOwnerId: s?.ownerId ?? '', title: s?.name ?? v.title }))
      }}><option value="">{t('handoff.choose')}</option>{options.sources.map(s => <option key={s.id} value={s.id}>{s.key} · {s.name}</option>)}</select></Field>
      <Field label={t('handoff.revision')} htmlFor="hf-revision"><Input id="hf-revision" required maxLength={100} value={values.declaredRevision} onChange={e => update('declaredRevision', e.target.value)} /></Field>
      <Field label={t('handoff.sourceLink')} htmlFor="hf-url"><Input id="hf-url" type="url" required maxLength={2000} value={values.sourceUrl} onChange={e => update('sourceUrl', e.target.value)} /></Field>
      <p className="text-xs text-muted-foreground sm:col-span-2">{t('handoff.manualRevision')}</p>
      <Field label={t('handoff.receivingDiscipline')} htmlFor="hf-discipline"><select id="hf-discipline" className={selectCls} required disabled={fixed} value={values.receivingDisciplineId} onChange={e => set(v => ({ ...v, receivingDisciplineId: e.target.value, target: '', receivingOwnerId: '' }))}>
        <option value="">{t('handoff.choose')}</option>{options.disciplines.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}</select></Field>
      <Field label={t('handoff.target')} htmlFor="hf-target"><select id="hf-target" className={selectCls} required disabled={fixed || !values.receivingDisciplineId} value={values.target} onChange={e => {
        const value = e.target.value; const w = value.startsWith('Task:') ? options.tasks.find(x => `Task:${x.id}` === value) : options.sources.find(x => `Deliverable:${x.id}` === value)
        set(v => ({ ...v, target: value, receivingOwnerId: w?.ownerId ?? v.receivingOwnerId, neededBy: w?.dueDate ?? v.neededBy }))
      }}><option value="">{t('handoff.choose')}</option><optgroup label={t('handoff.tasks')}>{workOptions('Task', options.tasks)}</optgroup><optgroup label={t('handoff.deliverables')}>{workOptions('Deliverable', options.sources)}</optgroup></select></Field>
      <Field label={t('handoff.sender')} htmlFor="hf-sender"><PersonSelect id="hf-sender" value={values.sendingOwnerId} people={options.people} onChange={v => update('sendingOwnerId', v)} /></Field>
      <Field label={t('handoff.receiver')} htmlFor="hf-receiver"><PersonSelect id="hf-receiver" value={values.receivingOwnerId} people={options.people} onChange={v => update('receivingOwnerId', v)} /></Field>
      <Field label={t('handoff.purpose')} htmlFor="hf-purpose" className="sm:col-span-2"><Textarea id="hf-purpose" required readOnly={fixed} maxLength={2000} value={values.intendedUse} onChange={e => update('intendedUse', e.target.value)} /></Field>
      <Field label={t('handoff.criteria')} htmlFor="hf-criteria" className="sm:col-span-2"><Textarea id="hf-criteria" required readOnly={fixed} maxLength={4000} value={values.acceptanceCriteria} onChange={e => update('acceptanceCriteria', e.target.value)} /></Field>
      <Field label={t('handoff.needed')} htmlFor="hf-needed"><Input id="hf-needed" type="date" required value={values.neededBy} onChange={e => update('neededBy', e.target.value)} /></Field>
      <Field label={t('handoff.promised')} htmlFor="hf-promised" hint={t('handoff.promiseHint')}><Input id="hf-promised" type="date" value={values.promisedBy} onChange={e => update('promisedBy', e.target.value)} /></Field>
      {values.promisedBy > values.neededBy && <p role="status" className="text-sm text-warn sm:col-span-2">{t('handoff.dateMismatchHint')}</p>}
      {existing && <Field label={t('handoff.reason')} htmlFor="hf-reason" className="sm:col-span-2"><Textarea id="hf-reason" value={values.reason} onChange={e => update('reason', e.target.value)} /></Field>}
      {error && <div className="sm:col-span-2"><ErrorBanner error={error} /><ul className="mt-2 text-sm text-bad">{Object.entries(error.fieldErrors).map(([key, messages]) => <li key={key}>{messages.join(' ')}</li>)}</ul></div>}
      <DialogFooter className="sm:col-span-2"><Button type="button" variant="outline" disabled={busy} onClick={() => onClose()}>{t('common.cancel')}</Button><Button type="submit" disabled={busy}>{busy && <Spinner />}{t('handoff.saveDraft')}</Button></DialogFooter>
    </form>
  </DialogContent></Dialog>
}

function HandoffDetail({ projectId, projectNumber, id, options, onClose, refresh }: { projectId: string; projectNumber: string; id: string; options?: Options; onClose: () => void; refresh: () => void }) {
  const query = useQuery({ queryKey: ['handoff-detail', projectId, id], queryFn: () => get<Detail>(`projects/${projectId}/handoffs/${id}`) })
  const [edit, setEdit] = useState(false)
  const [action, setAction] = useState<string | null>(null)
  const d = query.data
  if (edit && d && options) return <HandoffForm projectId={projectId} options={options} existing={d.row} onClose={() => { setEdit(false); refresh() }} />
  return <Dialog open onOpenChange={o => !o && onClose()}><DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-2xl">
    <DialogHeader><DialogTitle>{d ? `${d.row.key} · ${d.row.title}` : t('handoff.title')}</DialogTitle><DialogDescription>{t('handoff.receiptHint')}</DialogDescription></DialogHeader>
    {query.isPending ? <Loading rows={6} /> : query.error ? <ErrorBanner error={query.error} retry={() => query.refetch()} /> : d && <>
      <div className="flex flex-wrap gap-2"><span className="rounded border bg-muted px-2 py-1 text-sm">{tv(d.row.status)}</span>
        {d.permissions.edit.ok && options && <Button size="sm" variant="outline" onClick={() => setEdit(true)}>{t('handoff.edit')}</Button>}
        {d.permissions.assign.ok && options && !['Cancelled', 'Incorporated'].includes(d.row.status) && <Button size="sm" variant="outline" onClick={() => setAction('assign')}>{t('handoff.reassign')}</Button>}
        {d.permissions.transitions.map(a => <Button size="sm" key={a.to} variant="outline" disabled={!a.permission.ok} title={a.permission.reason} onClick={() => setAction(a.to)}>{t(`handoff.action.${a.to}`)}</Button>)}
      </div>
      {!d.row.ownersAvailable && <p role="status" className="rounded border border-warn/40 p-3 text-sm">{t('handoff.ownerUnavailable')}</p>}
      {d.row.sourceChanged && <p role="status" className="rounded border border-warn/40 p-3 text-sm">{t('handoff.sourceChangedHint', { revision: d.row.declaredRevision })}</p>}
      <dl className="grid grid-cols-2 gap-3 text-sm">
        <div><dt className="text-muted-foreground">{t('handoff.sender')}</dt><dd>{d.row.senderName}</dd></div><div><dt className="text-muted-foreground">{t('handoff.receiver')}</dt><dd>{d.row.receiverName}</dd></div>
        <div><dt className="text-muted-foreground">{t('handoff.source')}</dt><dd><Link className="text-primary underline" to={`/projects/${projectNumber}/deliverables?panel=Deliverable:${d.row.sourceDeliverableId}`}>{d.row.sourceKey}</Link> · {d.row.declaredRevision}</dd></div>
        <div><dt className="text-muted-foreground">{t('handoff.target')}</dt><dd><Link className="text-primary underline" to={`/projects/${projectNumber}/${d.row.targetTaskId ? 'tasks' : 'deliverables'}?panel=${d.row.targetTaskId ? 'Task' : 'Deliverable'}:${d.row.targetTaskId ?? d.row.targetDeliverableId}`}>{d.row.targetKey}</Link></dd></div>
        <div><dt className="text-muted-foreground">{t('handoff.needed')}</dt><dd>{fmtDate(d.row.neededBy)}</dd></div><div><dt className="text-muted-foreground">{t('handoff.promised')}</dt><dd>{fmtDate(d.row.promisedBy)}{d.row.dateMismatch && <span className="ml-2 text-warn">{t('handoff.dateMismatch')}</span>}</dd></div>
      </dl>
      <section><h3 className="text-sm font-medium">{t('handoff.purpose')}</h3><p className="whitespace-pre-wrap text-sm">{d.row.intendedUse}</p></section>
      <section><h3 className="text-sm font-medium">{t('handoff.criteria')}</h3><p className="whitespace-pre-wrap text-sm">{d.row.acceptanceCriteria}</p></section>
      <a href={d.row.sourceUrl} target="_blank" rel="noreferrer noopener" className="text-sm text-primary underline">{t('handoff.openSource')}</a><p className="text-xs text-muted-foreground">{t('handoff.manualRevision')}</p>
      {!!d.changeAssessments?.length && <section className="rounded border p-3 text-sm"><h3 className="font-medium">{t('change.assessments')}</h3><p className="mt-1 text-muted-foreground">{t('change.receiptPreserved')}</p>{d.changeAssessments.map(a => <p className="mt-2" key={a.id}><Link className="text-primary underline" to={`/projects/${projectNumber}/changes?panel=ChangeNotice:${a.changeNoticeId}`}>{a.key} · {a.title}</Link> · {tv(a.status)} · {tv(a.noticeStatus)}</p>)}</section>}
      <section className="border-t pt-3"><h3 className="font-medium">{t('handoff.history')}</h3>{!d.history.length && <p className="text-sm text-muted-foreground">{t('handoff.noHistory')}</p>}
        <ol className="mt-2 space-y-3">{d.history.map(e => <li key={e.id} className="rounded border p-3 text-sm"><div className="font-medium">{tv(e.fromStatus)} → {tv(e.toStatus)}</div><p className="text-xs text-muted-foreground">{e.actor} · {new Date(e.createdAt).toLocaleString()}</p>{e.reason && <p className="mt-1 whitespace-pre-wrap">{e.reason}</p>}{e.criteriaOutcome && <p className="mt-1 whitespace-pre-wrap">{e.criteriaOutcome}</p>}<p className="text-xs text-muted-foreground">{t('handoff.revision')}: {d.revisions.find(r => r.id === e.revisionId)?.source.revision ?? '—'}</p></li>)}</ol>
      </section>
      {!!d.revisions.length && <section className="border-t pt-3"><h3 className="font-medium">{t('handoff.submissions')}</h3>{d.revisions.map(r => <details key={r.id} className="mt-2 rounded border p-3 text-sm"><summary className="cursor-pointer">{r.source.sourceKey} · {r.source.revision} · {new Date(r.createdAt).toLocaleString()}{d.row.incorporatedRevisionId === r.id ? ` · ${tv('Incorporated')}` : ''}</summary><p className="mt-2 whitespace-pre-wrap">{r.intendedUse}</p><p className="whitespace-pre-wrap">{r.acceptanceCriteria}</p>{r.response && <p>{r.response}</p>}<a href={r.source.url} target="_blank" rel="noreferrer noopener" className="text-primary underline">{t('handoff.openSource')}</a></details>)}</section>}
      {action && <ActionDialog projectId={projectId} row={d.row} action={action} people={options?.people ?? []} onClose={() => setAction(null)} onDone={() => { setAction(null); refresh() }} />}
    </>}
  </DialogContent></Dialog>
}

function ActionDialog({ projectId, row, action, people, onClose, onDone }: { projectId: string; row: Row; action: string; people: Person[]; onClose: () => void; onDone: () => void }) {
  const [reason, setReason] = useState(''); const [outcome, setOutcome] = useState('')
  const [sender, setSender] = useState(row.sendingOwnerId); const [receiver, setReceiver] = useState(row.receivingOwnerId)
  const [busy, setBusy] = useState(false); const [error, setError] = useState<ApiError | null>(null)
  const receipt = useRef<Receipt | null>(null)
  const assign = action === 'assign'
  const needsOutcome = ['Accepted', 'Incorporated'].includes(action)
  const needsReason = assign || ['Returned', 'Clarification Requested', 'Cancelled'].includes(action) || (action === 'Submitted' && row.status !== 'Draft')
  const run = async () => {
    setBusy(true); setError(null)
    try {
      const payload = { toStatus: action, rowVersion: row.rowVersion, reason, criteriaOutcome: outcome, sendingOwnerId: sender, receivingOwnerId: receiver }
      await post(`projects/${projectId}/handoffs/${row.id}/${assign ? 'assign' : 'transition'}`, { ...payload, requestId: requestId(receipt, payload) })
      toast.success(t('handoff.saved')); onDone()
    } catch (e) { setError(e as ApiError) } finally { setBusy(false) }
  }
  return <Dialog open onOpenChange={o => !o && !busy && onClose()}><DialogContent><DialogHeader><DialogTitle>{t(assign ? 'handoff.reassign' : `handoff.action.${action}`)}</DialogTitle><DialogDescription>{t('handoff.receiptHint')}</DialogDescription></DialogHeader>
    <form className="space-y-3" onSubmit={e => { e.preventDefault(); run() }}>
      {assign && <><Field label={t('handoff.sender')} htmlFor="ha-sender"><PersonSelect id="ha-sender" value={sender} onChange={setSender} people={people} /></Field><Field label={t('handoff.receiver')} htmlFor="ha-receiver"><PersonSelect id="ha-receiver" value={receiver} onChange={setReceiver} people={people} /></Field></>}
      {needsOutcome && <Field label={t(action === 'Accepted' ? 'handoff.criteriaOutcome' : 'handoff.incorporationEvidence')} htmlFor="ha-outcome" hint={row.acceptanceCriteria}><Textarea id="ha-outcome" required maxLength={4000} value={outcome} onChange={e => setOutcome(e.target.value)} /></Field>}
      <Field label={t('handoff.reason')} htmlFor="ha-reason"><Textarea id="ha-reason" required={needsReason} maxLength={4000} value={reason} onChange={e => setReason(e.target.value)} /></Field>
      {error && <><ErrorBanner error={error} /><ul className="text-sm text-bad">{Object.entries(error.fieldErrors).map(([key, messages]) => <li key={key}>{messages.join(' ')}</li>)}</ul></>}
      <DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={onClose}>{t('common.cancel')}</Button><Button disabled={busy} type="submit">{busy && <Spinner />}{t('handoff.confirm')}</Button></DialogFooter>
    </form>
  </DialogContent></Dialog>
}
