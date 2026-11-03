import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import { useWorkSeenDetailOwner, WorkSeenDetailNotice } from '@/components/hub/work-seen-detail-owner'
import type { WorkSeenFields } from '@/components/hub/work-seen'
import { personOptionLabel } from '@/components/hub/person-option-label'
import { recordContextReturn } from '@/lib/record-context-navigation'
import { UrlSearchInput } from '@/components/hub/url-search'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ExternalLink, Plus } from 'lucide-react'
import { useRef, useState } from 'react'
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { ActiveFilters, ChipToggle, Empty, ErrorBanner, Field, FilterBar, Loading, Missing, Page, Spinner, TableRegion, selectCls, tdCls, thCls, useIsPhone } from '@/components/hub/common'
import { ExportMenu } from '@/components/hub/export'
import { Avatar } from '@/components/hub/people'
import { ViewMenu } from '@/components/hub/views'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import type { components } from '@/gen/api'
import { ApiError, typedRequest } from '@/lib/api'
import { invalidateChange } from '@/lib/keys'
import { addDays, fmtDate, today } from '@/lib/format'
import { t, tv , runtimeText } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { useCurrentProject } from './ProjectLayout'
import { CoordStatus, RegisterPager } from './CoordinationForms'
import { DraftNotice } from '@/components/hub/drafts'
import { useSafeForm } from '@/hooks/drafts'
import { RecordContext } from '@/components/hub/record-context'
import { useItemPanel } from '@/components/hub/panel-host'
import { useFlag, useMe } from '@/lib/auth'
import { useWorkRecordOrigin } from './work-record-navigation'
import { HandoffReadinessSummary } from './handoff-consequences'

const STATUSES = ['Draft', 'Submitted', 'Clarification Requested', 'Returned', 'Accepted', 'Incorporated', 'Cancelled']
type Schemas = components['schemas']
type Person = Schemas['HandoffPerson']
type Work = Schemas['HandoffTask']
type Options = Schemas['HandoffOptions']
type Row = Schemas['HandoffRow']
type Receipt = { signature: string; id: string }
// Retain the command ID on network retry; changing any input creates a new command.
function requestId(ref: { current: Receipt | null }, payload: unknown) {
  const signature = JSON.stringify(payload)
  if (!ref.current || ref.current.signature !== signature) ref.current = { signature, id: crypto.randomUUID() }
  return ref.current.id
}
/** Text that flags a condition with the §13.0 symbol, never colour alone. */
const Flag = ({ tone, children }: { tone: 'warn' | 'bad'; children: string }) => <p className={cn('mt-1 text-xs/[18px]', tone === 'bad' ? 'text-bad' : 'text-warn')}><span aria-hidden>{tone === 'bad' ? '■ ' : '▲ '}</span>{children}</p>
/** Field errors returned with a refusal, listed under the banner exactly as the server states them. */
function Refusal({ error }: { error: ApiError }) {
  const messages = Object.entries(error.fieldErrors)
  return <><ErrorBanner error={error} />{messages.length > 0 && <ul className="mt-2 space-y-1 rounded-md border border-bad/30 bg-bad-bg px-4 py-3 text-sm text-bad">{messages.map(([key, m]) => <li key={key}>{m.join(' ')}</li>)}</ul>}</>
}

export function HandoffsTab() {
  const phone = useIsPhone()
  const p = useCurrentProject()
  const [sp, setSp] = useSearchParams()
  const location = useLocation()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [create, setCreate] = useState(sp.has('source') || sp.has('targetTask'))
  const registerOn = useFlag('register_screens'), viewer = useMe()
  const filters = Object.fromEntries(['q', 'status', 'direction', 'disciplineId', 'overdue'].map(k => [k, sp.get(k) ?? '']))
  const page = Math.max(1, Number(sp.get('page')) || 1)
  const list = useQuery({ queryKey: ['handoffs', p.id, filters, page, viewer.id, registerOn], queryFn: () => typedRequest('get', '/api/v1/projects/{projectId}/handoffs', { params: { projectId: p.id },
    query: { Q: filters.q, Status: filters.status, Direction: filters.direction, DisciplineId: filters.disciplineId, Overdue: filters.overdue === 'true', page } }) })
  const options = useQuery({ queryKey: ['handoff-options', p.id], queryFn: () => typedRequest('get', '/api/v1/projects/{projectId}/handoffs/options', { params: { projectId: p.id } }) })
  const panel = sp.get('panel')?.startsWith('Handoff:') ? sp.get('panel')!.slice(8) : null
  const setFilter = (key: string, value: string) => { const next = new URLSearchParams(sp); if (value) next.set(key, value); else next.delete(key); next.delete('page'); setSp(next) }
  const refresh = () => { invalidateChange(qc, 'Handoff', p.id) }
  const open = (id: string) => { const next = new URLSearchParams(sp); next.set('panel', `Handoff:${id}`); setSp(next) }
  const close = () => {
    const returned = recordContextReturn(location.state)
    if (returned) { navigate(returned.url, { replace: true, state: returned.state }); return }
    const next = new URLSearchParams(sp); next.delete('panel'); setSp(next)
  }
  // Active filters as removable tokens (§13.0 Filters); the URL keeps the same parameters.
  const tokens = [
    filters.q && { key: 'q', label: t('common.search'), value: filters.q },
    filters.direction && { key: 'direction', label: t('handoff.direction'), value: filters.direction === 'incoming' ? t('handoff.incoming') : filters.direction === 'outgoing' ? t('handoff.outgoing') : filters.direction },
    filters.status && { key: 'status', label: t('common.status'), value: tv(filters.status) },
    filters.disciplineId && { key: 'disciplineId', label: t('handoff.discipline'), value: options.data?.disciplines.find(d => d.id === filters.disciplineId)?.name ?? t('common.dash') },
    filters.overdue && { key: 'overdue', label: t('handoff.overdueOnly'), value: t('common.yes') },
  ].filter(x => !!x)
  const clear = () => { const next = new URLSearchParams(sp); for (const k of Object.keys(filters)) next.delete(k); next.delete('page'); setSp(next) }
  const setPage = (n: number) => { const next = new URLSearchParams(sp); next.set('page', String(n)); setSp(next) }
  const discipline = (id: string) => options.data?.disciplines.find(d => d.id === id)?.name ?? t('coord.unavailable')
  return <Page title={t('handoff.title')} subtitle={t('handoff.subtitle')} actions={<>
    <ViewMenu listType="handoffs" projectId={p.id} />
    <ExportMenu path={`projects/${p.id}/handoffs/export`} params={filters} name={`${p.projectNumber}-handoffs`} />
    {options.data?.canCreate && <Button onClick={() => setCreate(true)}><Plus className="size-4" />{t('handoff.new')}</Button>}
  </>}>
    <FilterBar activeCount={tokens.length}
      search={<Field label={t('common.search')} htmlFor="handoff-search"><UrlSearchInput id="handoff-search" placeholder={t('common.search')} value={filters.q} onValueChange={value => setFilter('q', value)} /></Field>}
      quick={<ChipToggle on={filters.overdue === 'true'} onClick={() => setFilter('overdue', filters.overdue === 'true' ? '' : 'true')}>{t('handoff.overdueOnly')}</ChipToggle>}
      active={<ActiveFilters tokens={tokens} onRemove={k => setFilter(k, '')} onClear={clear} />}>
      <div className="flex flex-wrap items-end gap-3">
        <Field label={t('handoff.direction')} htmlFor="handoff-direction" className="w-full sm:w-44"><select id="handoff-direction" className={selectCls} value={filters.direction} onChange={e => setFilter('direction', e.target.value)}>
          <option value="">{t('handoff.all')}</option><option value="incoming">{t('handoff.incoming')}</option><option value="outgoing">{t('handoff.outgoing')}</option>
        </select></Field>
        <Field label={t('common.status')} htmlFor="handoff-status" className="w-full sm:w-48"><select id="handoff-status" className={selectCls} value={filters.status} onChange={e => setFilter('status', e.target.value)}>
          <option value="">{t('handoff.all')}</option>{STATUSES.map(s => <option key={s} value={s}>{tv(s)}</option>)}
        </select></Field>
        <Field label={t('handoff.discipline')} htmlFor="handoff-discipline" className="w-full sm:w-48"><select id="handoff-discipline" className={selectCls} value={filters.disciplineId} onChange={e => setFilter('disciplineId', e.target.value)}>
          <option value="">{t('handoff.all')}</option>{options.data?.disciplines.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
        </select></Field>
      </div>
    </FilterBar>
    {options.error && <ErrorBanner error={options.error} retry={() => options.refetch()} />}
    {list.isPending ? <div className="rounded-lg border bg-card"><Loading rows={5} /></div> : list.error ? <ErrorBanner error={list.error} retry={() => list.refetch()} /> : <>
      <p role="status" className="text-sm text-muted-foreground">{t('handoff.count', { n: list.data.totalCount })}</p>
      {!list.data.items.length ? <div className="rounded-lg border bg-card"><Empty title={t('handoff.empty')}>{t(tokens.length ? 'register.noMatch' : 'handoff.emptyHint')}</Empty></div> :
        phone ? <ul className="space-y-2">{list.data.items.map(h => <li key={h.id} className={cn('space-y-3 rounded-lg border bg-card p-4 text-sm', panel === h.id && 'bg-accent shadow-[inset_3px_0_0_var(--primary)]')}><button className="break-words text-left font-semibold text-primary hover:underline" aria-current={panel === h.id || undefined} onClick={() => open(h.id)}><span className="key font-normal">{h.key}</span> · {h.title}</button><CoordStatus status={h.status} /><p className="break-words text-xs/[18px] text-muted-foreground"><span className="key">{h.sourceKey}</span> → <span className="key">{h.targetKey}</span><br />{discipline(h.sendingDisciplineId)} → {discipline(h.receivingDisciplineId)}</p><div className="space-y-1"><div className="flex items-center gap-2"><Avatar id={h.sendingOwnerId} name={h.senderName} />{t('handoff.from', { name: '' }).trim()} {h.sendingOwnerId && h.senderName ? <PersonPanelTrigger userId={h.sendingOwnerId} name={h.senderName} focusKey={`handoff:${h.id}:sender:card`} /> : h.senderName ?? t('common.unavailable')}</div><div className="flex items-center gap-2"><Avatar id={h.receivingOwnerId} name={h.receiverName} />{t('handoff.to', { name: '' }).trim()} {h.receivingOwnerId && h.receiverName ? <PersonPanelTrigger userId={h.receivingOwnerId} name={h.receiverName} focusKey={`handoff:${h.id}:receiver:card`} /> : h.receiverName ?? t('common.unavailable')}</div>{!h.ownersAvailable && <Flag tone="bad">{t('handoff.ownerUnavailable')}</Flag>}</div><dl className="grid grid-cols-[minmax(5rem,auto)_minmax(0,1fr)] gap-x-3 gap-y-2"><dt className="text-muted-foreground">{t('handoff.revision')}</dt><dd>{h.declaredRevision}{h.sourceChanged && <Flag tone="warn">{t('handoff.sourceChanged')}</Flag>}</dd><dt className="text-muted-foreground">{t('handoff.needed')}</dt><dd>{fmtDate(h.neededBy)}{h.isOverdue && <Flag tone="bad">{t('handoff.overdue')}</Flag>}</dd><dt className="text-muted-foreground">{t('handoff.promised')}</dt><dd>{h.promisedBy ? fmtDate(h.promisedBy) : <Missing />}{h.dateMismatch && <Flag tone="warn">{t('handoff.dateMismatch')}</Flag>}</dd></dl></li>)}</ul> :
        <TableRegion><table className="w-full text-left text-sm"><caption className="sr-only">{t('handoff.title')}</caption>
          <thead className="bg-muted"><tr>{['item', 'owners', 'revision', 'needed', 'promised', 'state'].map(k => <th key={k} scope="col" className={thCls}>{runtimeText(`handoff.${k}`)}</th>)}</tr></thead>
          <tbody>{list.data.items.map(h => <tr key={h.id} className={cn('border-t hover:bg-muted', panel === h.id && 'bg-accent shadow-[inset_3px_0_0_var(--primary)] hover:bg-accent')}>
            <td className={cn(tdCls, 'min-w-64')}><button className="break-words text-left font-semibold text-primary underline-offset-4 hover:underline" aria-current={panel === h.id || undefined} onClick={() => open(h.id)}><span className="key font-normal">{h.key}</span> · {h.title}</button><p className="mt-1 text-xs/[18px] text-muted-foreground"><span className="key">{h.sourceKey}</span> → <span className="key">{h.targetKey}</span></p>
              {options.data && <p className="text-xs/[18px] text-muted-foreground">{discipline(h.sendingDisciplineId)} → {discipline(h.receivingDisciplineId)}</p>}</td>
            <td className={tdCls}><div className="space-y-1.5">
              <div className="flex items-center gap-2"><Avatar id={h.sendingOwnerId} name={h.senderName} /><span>{t('handoff.from', { name: '' }).trim()} {h.sendingOwnerId && h.senderName ? <PersonPanelTrigger userId={h.sendingOwnerId} name={h.senderName} focusKey={`handoff:${h.id}:sender:table`} /> : h.senderName ?? t('common.unavailable')}</span></div>
              <div className="flex items-center gap-2"><Avatar id={h.receivingOwnerId} name={h.receiverName} /><span>{t('handoff.to', { name: '' }).trim()} {h.receivingOwnerId && h.receiverName ? <PersonPanelTrigger userId={h.receivingOwnerId} name={h.receiverName} focusKey={`handoff:${h.id}:receiver:table`} /> : h.receiverName ?? t('common.unavailable')}</span></div>
            </div>{!h.ownersAvailable && <Flag tone="bad">{t('handoff.ownerUnavailable')}</Flag>}</td>
            <td className={cn(tdCls, 'max-w-48')}>{h.declaredRevision}{h.sourceChanged && <Flag tone="warn">{t('handoff.sourceChanged')}</Flag>}</td>
            <td className={cn(tdCls, 'whitespace-nowrap tabular-nums')}>{fmtDate(h.neededBy)}{h.isOverdue && <Flag tone="bad">{t('handoff.overdue')}</Flag>}</td>
            <td className={cn(tdCls, 'whitespace-nowrap tabular-nums')}>{h.promisedBy ? fmtDate(h.promisedBy) : <Missing />}{h.dateMismatch && <Flag tone="warn">{t('handoff.dateMismatch')}</Flag>}</td>
            <td className={tdCls}><CoordStatus status={h.status} /></td>
          </tr>)}</tbody></table></TableRegion>}
      <RegisterPager label={t('handoff.pager')} page={page} total={list.data.totalCount} pageSize={list.data.pageSize} onPage={setPage} />
    </>}
    {create && options.data && <HandoffForm projectId={p.id} options={options.data} initialSource={sp.get('source')} initialTask={sp.get('targetTask')} onClose={(id) => {
      setCreate(false); refresh(); const next = new URLSearchParams(sp); next.delete('source'); next.delete('targetTask'); if (id) next.set('panel', `Handoff:${id}`); setSp(next)
    }} />}
    {panel && !create && <HandoffDetail projectId={p.id} projectNumber={p.projectNumber} id={panel} options={options.data} onClose={close} refresh={refresh} />}
  </Page>
}

function PersonSelect({ id, value, onChange, people }: { id: string; value: string; onChange: (value: string) => void; people: Person[] }) {
  const noticesOn = useFlag('capacity_screens')
  return <select id={id} className={selectCls} required value={value} onChange={e => onChange(e.target.value)}><option value="">{t('handoff.choose')}</option>{people.map(p => <option key={p.id} value={p.id}>{personOptionLabel(p.displayName, p.personNotices, noticesOn)}</option>)}</select>
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
  const safe = useSafeForm(existing ? null : `handoff.new.${projectId}`, values, set) // FR-SAV-03
  const [error, setError] = useState<ApiError | null>(null)
  const receipt = useRef<Receipt | null>(null)
  const fixed = !!existing?.currentRevisionId
  const selected = options.sources.find(s => s.id === values.sourceDeliverableId)
  const save = async () => {
    setBusy(true); setError(null)
    try {
      const payload = { ...values, sourceRowVersion: selected?.rowVersion ?? existing?.sourceRowVersion ?? 0,
        targetTaskId: values.target.startsWith('Task:') ? values.target.slice(5) : null, targetDeliverableId: values.target.startsWith('Deliverable:') ? values.target.slice(12) : null,
        promisedBy: values.promisedBy || null, rowVersion: existing?.rowVersion ?? null }
      const body = { ...payload, requestId: requestId(receipt, payload) }
      const result = existing ? await typedRequest('post', '/api/v1/projects/{projectId}/handoffs/{id}/draft', { params: { projectId, id: existing.id }, body })
        : await typedRequest('post', '/api/v1/projects/{projectId}/handoffs', { params: { projectId }, body })
      safe.clear(); toast.success(t('handoff.saved')); onClose(result.id)
    } catch (e) { setError(e as ApiError) } finally { setBusy(false) }
  }
  const workOptions = (kind: string, work: Work[]) => work.filter(w => w.projectDisciplineId === values.receivingDisciplineId).map(w => <option key={w.id} value={`${kind}:${w.id}`}>{w.key} · {w.name}</option>)
  return <Dialog open onOpenChange={o => !o && !busy && safe.close(onClose)}><DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-2xl">
    <DialogHeader><DialogTitle>{t(existing ? 'handoff.edit' : 'handoff.new')}</DialogTitle><DialogDescription>{t('handoff.formHint')}</DialogDescription></DialogHeader>
    <DraftNotice form={safe} />
    <form onSubmit={e => { e.preventDefault(); save() }} className="grid gap-4 sm:grid-cols-2">
      <Field label={t('handoff.item')} htmlFor="hf-title" className="sm:col-span-2"><Input id="hf-title" required maxLength={200} value={values.title} onChange={e => update('title', e.target.value)} /></Field>
      <Field label={t('handoff.source')} htmlFor="hf-source" className="sm:col-span-2"><select id="hf-source" className={selectCls} required disabled={!!existing} value={values.sourceDeliverableId} onChange={e => {
        const s = options.sources.find(x => x.id === e.target.value); set(v => ({ ...v, sourceDeliverableId: e.target.value, declaredRevision: s?.revision ?? '', sourceUrl: s?.transmittalUrl ?? '', sendingOwnerId: s?.ownerId ?? '', title: s?.name ?? v.title }))
      }}><option value="">{t('handoff.choose')}</option>{options.sources.map(s => <option key={s.id} value={s.id}>{s.key} · {s.name}</option>)}</select></Field>
      <Field label={t('handoff.revision')} htmlFor="hf-revision"><Input id="hf-revision" required maxLength={100} value={values.declaredRevision} onChange={e => update('declaredRevision', e.target.value)} /></Field>
      <Field label={t('handoff.sourceLink')} htmlFor="hf-url"><Input id="hf-url" type="url" required maxLength={2000} value={values.sourceUrl} onChange={e => update('sourceUrl', e.target.value)} /></Field>
      <p className="-mt-2 text-xs/[18px] text-muted-foreground sm:col-span-2">{t('handoff.manualRevision')}</p>
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
      {values.promisedBy > values.neededBy && <p role="status" className="flex gap-2 rounded-md border border-warn/40 bg-warn-bg px-4 py-3 text-sm text-warn sm:col-span-2"><span aria-hidden>▲</span><span>{t('handoff.dateMismatchHint')}</span></p>}
      {existing && <Field label={t('handoff.reason')} htmlFor="hf-reason" className="sm:col-span-2"><Textarea id="hf-reason" value={values.reason} onChange={e => update('reason', e.target.value)} /></Field>}
      {error && <div className="sm:col-span-2"><Refusal error={error} /></div>}
      <DialogFooter className="sm:col-span-2"><Button type="button" variant="outline" disabled={busy} onClick={() => safe.close(onClose)}>{t('common.cancel')}</Button><Button type="submit" disabled={busy}>{busy && <Spinner />}{busy ? t('common.saving') : t('handoff.saveDraft')}</Button></DialogFooter>
    </form>
  </DialogContent></Dialog>
}

function HandoffDetail({ projectId, projectNumber, id, options, onClose, refresh }: { projectId: string; projectNumber: string; id: string; options?: Options; onClose: () => void; refresh: () => void }) {
  const recordOrigin = useWorkRecordOrigin()
  const contextOn = useFlag('screen_patterns')
  const me = useMe()
  const workOn = useFlag('work_screens'), peopleOn = useFlag('people_screens'), registerOn = useFlag('register_screens')
  const openPanel = useItemPanel()
  const query = useQuery({ queryKey: ['handoff-detail', projectId, id, me.id, workOn, peopleOn, registerOn], queryFn: async () => await typedRequest('get', '/api/v1/projects/{projectId}/handoffs/{id}', { params: { projectId, id } }) as Schemas['HandoffDetail'] & WorkSeenFields })
  const [edit, setEdit] = useState(false)
  const [action, setAction] = useState<string | null>(null)
  const d = query.data
  const [detailParams] = useSearchParams()
  const seenOwner = useWorkSeenDetailOwner({ itemType: 'Handoff', itemId: d?.row.id ?? id,
    sourceVersion: d?.row.rowVersion, seen: d?.seen, read: query,
    visible: detailParams.get('panel') === `Handoff:${id}` && !edit && !action })
  if (edit && d && options) return <HandoffForm projectId={projectId} options={options} existing={d.row} onClose={() => { setEdit(false); refresh() }} />
  // The next step is the first permitted forward transition; the others stay secondary (one primary per dialog).
  const next = d?.permissions.transitions.find(a => a.permission.ok && a.to !== 'Cancelled')?.to
  const refused = d?.permissions.transitions.filter(a => !a.permission.ok && a.permission.reason) ?? []
  return <Dialog open onOpenChange={o => !o && onClose()}><DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-2xl">
    <DialogHeader><DialogTitle>{d ? `${d.row.key} · ${d.row.title}` : t('handoff.title')}</DialogTitle><DialogDescription>{t('handoff.receiptHint')}</DialogDescription></DialogHeader>
    {query.isPending ? <Loading rows={6} /> : query.error ? <ErrorBanner error={query.error} retry={() => query.refetch()} /> : d && <div ref={seenOwner.surfaceRef} className="space-y-5 text-sm">
      <WorkSeenDetailNotice state={seenOwner.state} />
      {contextOn && <RecordContext context={d.context} projectNumber={projectNumber} meId={me.id} openPanel={openPanel} />}
      <div className="space-y-2">
        <div className="flex flex-wrap items-center gap-2"><CoordStatus status={d.row.status} /><span className="flex-1" />
          {d.permissions.transitions.map(a => <Button size="sm" key={a.to} variant={a.to === next ? 'default' : a.to === 'Cancelled' ? 'ghost' : 'outline'} className={cn(a.to === 'Cancelled' && 'text-bad hover:text-bad')} disabled={!a.permission.ok} title={a.permission.reason ?? undefined} onClick={() => setAction(a.to)}>{runtimeText(`handoff.action.${a.to}`)}</Button>)}
          {d.permissions.edit.ok && options && <Button size="sm" variant="outline" onClick={() => setEdit(true)}>{t('handoff.edit')}</Button>}
          {d.permissions.assign.ok && options && !['Cancelled', 'Incorporated'].includes(d.row.status) && <Button size="sm" variant="outline" onClick={() => setAction('assign')}>{t('handoff.reassign')}</Button>}
        </div>
        {refused.length > 0 && <ul className="space-y-1 text-xs/[18px] text-muted-foreground">{refused.map(a => <li key={a.to}><span className="font-medium text-foreground">{runtimeText(`handoff.action.${a.to}`)}</span> · {a.permission.reason}</li>)}</ul>}
      </div>
      {!d.row.ownersAvailable && <p role="status" className="flex gap-2 rounded-md border border-bad/30 bg-bad-bg px-4 py-3 text-bad"><span aria-hidden>■</span><span>{t('handoff.ownerUnavailable')}</span></p>}
      {d.row.sourceChanged && <p role="status" className="flex gap-2 rounded-md border border-warn/40 bg-warn-bg px-4 py-3 text-warn"><span aria-hidden>▲</span><span>{t('handoff.sourceChangedHint', { revision: d.row.declaredRevision })}</span></p>}
      <dl className="grid gap-x-6 gap-y-3 sm:grid-cols-2">
        <div><dt className="text-muted-foreground">{t('handoff.sender')}</dt><dd className="mt-1 flex items-center gap-2"><Avatar id={d.row.sendingOwnerId} name={d.row.senderName} />{d.row.sendingOwnerId && d.row.senderName ? <PersonPanelTrigger userId={d.row.sendingOwnerId} name={d.row.senderName} focusKey={`handoff:${d.row.id}:sender:detail`} /> : d.row.senderName ?? t('common.unavailable')}</dd></div>
        <div><dt className="text-muted-foreground">{t('handoff.receiver')}</dt><dd className="mt-1 flex items-center gap-2"><Avatar id={d.row.receivingOwnerId} name={d.row.receiverName} />{d.row.receivingOwnerId && d.row.receiverName ? <PersonPanelTrigger userId={d.row.receivingOwnerId} name={d.row.receiverName} focusKey={`handoff:${d.row.id}:receiver:detail`} /> : d.row.receiverName ?? t('common.unavailable')}</dd></div>
        <div><dt className="text-muted-foreground">{t('handoff.source')}</dt><dd className="mt-1"><Link className="key text-primary underline underline-offset-4" onClick={recordOrigin} to={`/projects/${projectNumber}/deliverables?panel=Deliverable:${d.row.sourceDeliverableId}`}>{d.row.sourceKey}</Link> · {d.row.declaredRevision}</dd></div>
        <div><dt className="text-muted-foreground">{t('handoff.target')}</dt><dd className="mt-1"><Link className="key text-primary underline underline-offset-4" onClick={recordOrigin} to={`/projects/${projectNumber}/${d.row.targetTaskId ? 'tasks' : 'deliverables'}?panel=${d.row.targetTaskId ? 'Task' : 'Deliverable'}:${d.row.targetTaskId ?? d.row.targetDeliverableId}`}>{d.row.targetKey}</Link></dd></div>
        <div><dt className="text-muted-foreground">{t('handoff.needed')}</dt><dd className="mt-1 tabular-nums">{fmtDate(d.row.neededBy)}{d.row.isOverdue && <Flag tone="bad">{t('handoff.overdue')}</Flag>}</dd></div>
        <div><dt className="text-muted-foreground">{t('handoff.promised')}</dt><dd className="mt-1 tabular-nums">{d.row.promisedBy ? fmtDate(d.row.promisedBy) : <Missing />}{d.row.dateMismatch && <Flag tone="warn">{t('handoff.dateMismatch')}</Flag>}</dd></div>
      </dl>
      <HandoffReadinessSummary projectId={projectId} projectNumber={projectNumber} handoffId={id} links={d.row.readinessLinks} />
      <section className="space-y-1"><h3 className="font-semibold">{t('handoff.purpose')}</h3><p className="whitespace-pre-wrap text-base/6">{d.row.intendedUse}</p></section>
      <section className="space-y-1"><h3 className="font-semibold">{t('handoff.criteria')}</h3><p className="whitespace-pre-wrap text-base/6">{d.row.acceptanceCriteria}</p></section>
      <div className="space-y-1"><a href={d.row.sourceUrl} target="_blank" rel="noreferrer noopener" className="inline-flex items-center gap-1.5 text-primary underline underline-offset-4"><ExternalLink className="size-4" />{t('handoff.openSource')}</a><p className="text-xs/[18px] text-muted-foreground">{t('handoff.manualRevision')}</p></div>
      {!!d.changeAssessments.length && <section className="space-y-2 rounded-lg border p-4"><h3 className="font-semibold">{t('change.assessments')}</h3><p className="text-muted-foreground">{t('change.receiptPreserved')}</p><ul className="space-y-2">{d.changeAssessments.map(a => <li key={a.id} className="flex flex-wrap items-center gap-2"><Link className="text-primary underline underline-offset-4" onClick={recordOrigin} to={`/projects/${projectNumber}/changes?panel=ChangeNotice:${a.changeNoticeId}`}><span className="key">{a.key}</span> · {a.title}</Link><CoordStatus status={a.status} /><CoordStatus status={a.noticeStatus} /></li>)}</ul></section>}
      <section className="space-y-2 border-t pt-4"><h3 className="text-base/6 font-semibold">{t('handoff.history')}</h3>{!d.history.length && <p className="text-muted-foreground">{t('handoff.noHistory')}</p>}
        <ol className="space-y-3">{d.history.map(e => <li key={e.id} className="space-y-1 rounded-lg border p-4"><div className="font-medium">{tv(e.fromStatus)} → {tv(e.toStatus)}</div><p className="text-xs/[18px] text-muted-foreground">{e.createdBy && e.actor ? <PersonPanelTrigger userId={e.createdBy} name={e.actor} focusKey={`handoff:${d.row.id}:history:${e.id}:actor`} /> : e.actor ?? t('common.unavailable')} · <span className="tabular-nums">{new Date(e.createdAt).toLocaleString()}</span></p>{e.reason && <p className="whitespace-pre-wrap">{e.reason}</p>}{e.criteriaOutcome && <p className="whitespace-pre-wrap">{e.criteriaOutcome}</p>}<p className="text-xs/[18px] text-muted-foreground">{t('handoff.revision')}: {d.revisions.find(r => r.id === e.revisionId)?.source.revision ?? '—'}</p></li>)}</ol>
      </section>
      {!!d.revisions.length && <section className="space-y-2 border-t pt-4"><h3 className="text-base/6 font-semibold">{t('handoff.submissions')}</h3>{d.revisions.map(r => <details key={r.id} className="rounded-lg border px-4 py-2"><summary className="cursor-pointer py-1"><span className="key">{r.source.sourceKey}</span> · {r.source.revision} · <span className="tabular-nums">{new Date(r.createdAt).toLocaleString()}</span>{d.row.incorporatedRevisionId === r.id ? ` · ${tv('Incorporated')}` : ''}</summary><div className="space-y-1 pb-2 pt-1"><p className="whitespace-pre-wrap">{r.intendedUse}</p><p className="whitespace-pre-wrap">{r.acceptanceCriteria}</p>{r.response && <p>{r.response}</p>}<a href={r.source.url} target="_blank" rel="noreferrer noopener" className="text-primary underline underline-offset-4">{t('handoff.openSource')}</a></div></details>)}</section>}
      {action && <ActionDialog projectId={projectId} row={d.row} action={action} people={options?.people ?? []} onClose={() => setAction(null)} onDone={() => { setAction(null); refresh() }} />}
    </div>}
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
      const body = { ...payload, requestId: requestId(receipt, payload) }
      if (assign) await typedRequest('post', '/api/v1/projects/{projectId}/handoffs/{id}/assign', { params: { projectId, id: row.id }, body })
      else await typedRequest('post', '/api/v1/projects/{projectId}/handoffs/{id}/transition', { params: { projectId, id: row.id }, body })
      toast.success(t('handoff.saved')); onDone()
    } catch (e) { setError(e as ApiError) } finally { setBusy(false) }
  }
  return <Dialog open onOpenChange={o => !o && !busy && onClose()}><DialogContent><DialogHeader><DialogTitle>{runtimeText(assign ? 'handoff.reassign' : `handoff.action.${action}`)}</DialogTitle><DialogDescription>{t('handoff.receiptHint')}</DialogDescription></DialogHeader>
    <form className="space-y-4" onSubmit={e => { e.preventDefault(); run() }}>
      {assign && <><Field label={t('handoff.sender')} htmlFor="ha-sender"><PersonSelect id="ha-sender" value={sender} onChange={setSender} people={people} /></Field><Field label={t('handoff.receiver')} htmlFor="ha-receiver"><PersonSelect id="ha-receiver" value={receiver} onChange={setReceiver} people={people} /></Field></>}
      {needsOutcome && <Field label={t(action === 'Accepted' ? 'handoff.criteriaOutcome' : 'handoff.incorporationEvidence')} htmlFor="ha-outcome" hint={row.acceptanceCriteria}><Textarea id="ha-outcome" required maxLength={4000} value={outcome} onChange={e => setOutcome(e.target.value)} /></Field>}
      <Field label={t('handoff.reason')} htmlFor="ha-reason" optional={!needsReason}><Textarea id="ha-reason" required={needsReason} maxLength={4000} value={reason} onChange={e => setReason(e.target.value)} /></Field>
      {error && <Refusal error={error} />}
      <DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={onClose}>{t('common.cancel')}</Button><Button disabled={busy} type="submit">{busy && <Spinner />}{busy ? t('common.saving') : t('handoff.confirm')}</Button></DialogFooter>
    </form>
  </DialogContent></Dialog>
}
