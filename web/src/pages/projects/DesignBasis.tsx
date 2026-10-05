import { ViewMenu } from '@/components/hub/views'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { ActiveFilters, ChipToggle, Empty, ErrorBanner, Field, FilterBar, Loading, Page, Spinner, TableRegion, tdCls, thCls } from '@/components/hub/common'
import { ExportMenu } from '@/components/hub/export'
import { Pill } from '@/components/hub/pills'
import { ApiError, get, post } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { CommandForm, CoordStatus, Count, PersonLabel, RegisterPager, SelectField, type CoordOptions, workChoices, workRef, WorkLink } from './CoordinationForms'
import { useCurrentProject } from './ProjectLayout'

type EntryRow = { id: string; key: string; title: string; kind: string; ownerId: string; projectDisciplineId: string;
  currentVersionId?: string; rowVersion: number; currentStatus?: string; currentScope?: string; latestStatus?: string;
  latestVersionId?: string; confirmationDueDate?: string; conflictCount: number }
type Entry = EntryRow & { independentApproverId?: string }
type Version = { id: string; entryId: string; number: number; status: string; scope: string; statement: string;
  numericValue?: number; units?: string; sourceSystem?: string; stableSourceId?: string; sourceUrl?: string;
  declaredRevision?: string; confirmationDueDate?: string; rowVersion: number; supersedesVersionId?: string;
  confirmedBy?: string; confirmedAt?: string; confirmationRationale?: string; decisionId?: string }
type Detail = { entry: Entry; versions: { version: Version; sourceMissing: boolean }[];
  uses: { id: string; versionId: string; targetType: string; targetId: string; intendedUse: string; ownerId: string; rowVersion: number; isCurrent: boolean }[];
  impacts: { id: string; basisUseId: string; oldVersionId: string; newVersionId?: string; withdrawalVersionId?: string; status: string; ownerId: string; rowVersion: number; rationale?: string; evidenceUrl?: string }[];
  conflicts: { id: string; resolved: boolean; rowVersion: number; left: { versionId: string; entryKey: string; scope: string; statement: string; numericValue?: number; units?: string };
    right: { versionId: string; entryKey: string; scope: string; statement: string; numericValue?: number; units?: string } }[];
  dispositions: { id: string; versionId: string; scope: string; ownerId: string; approvedBy: string; expiresOn: string; reason: string }[];
  canManage: boolean; canEditProposed: boolean; canConfirm: boolean }
type Team = { members: { userId: string; displayName: string; primaryDisciplineId?: string }[] }
type VersionDraft = { scope: string; statement: string; numericValue: string; units: string; sourceSystem: string;
  stableSourceId: string; sourceUrl: string; declaredRevision: string; confirmationDueDate: string; decisionId: string }
const blank: VersionDraft = { scope: '', statement: '', numericValue: '', units: '', sourceSystem: '', stableSourceId: '',
  sourceUrl: '', declaredRevision: '', confirmationDueDate: '', decisionId: '' }
const fromVersion = (v?: Version): VersionDraft => v ? { scope: v.scope, statement: v.statement,
  numericValue: v.numericValue == null ? '' : String(v.numericValue), units: v.units ?? '', sourceSystem: v.sourceSystem ?? '',
  stableSourceId: v.stableSourceId ?? '', sourceUrl: v.sourceUrl ?? '', declaredRevision: v.declaredRevision ?? '',
  confirmationDueDate: v.confirmationDueDate ?? '', decisionId: v.decisionId ?? '' } : { ...blank }
const payloadVersion = (v: VersionDraft) => ({ scope: v.scope, statement: v.statement,
  numericValue: v.numericValue === '' ? null : Number(v.numericValue), units: v.units || null, sourceSystem: v.sourceSystem || null,
  stableSourceId: v.stableSourceId || null, sourceUrl: v.sourceUrl || null, declaredRevision: v.declaredRevision || null,
  confirmationDueDate: v.confirmationDueDate || null, decisionId: v.decisionId || null })

/** FR-BAS-04: flag current work that still relies on a replaced or withdrawn version. */
const staleUse = (status?: string) => status === 'Superseded' || status === 'Withdrawn'
  ? <strong className="ml-2 inline-flex items-center gap-1 font-semibold text-warn"><span aria-hidden>▲</span>{t(status === 'Withdrawn' ? 'basis.withdrawnUse' : 'basis.supersededUse')}</strong> : null
/** Kind is a category (identity tint), not a status. */
const Kind = ({ kind }: { kind: string }) => <span data-accent={kind === 'Assumption' ? 'amber' : 'blue'} className="inline-flex items-center whitespace-nowrap rounded-md bg-(--acc-bg) px-2 py-0.5 text-xs/[18px] font-medium text-(--acc-fg)">{kind === 'Assumption' ? t('basis.assumption') : kind === 'Criterion' ? t('basis.criterion') : kind}</span>
const Warn = ({ children }: { children: string }) => <p className="flex gap-2 text-warn"><span aria-hidden>▲</span><span>{children}</span></p>
const subhead = 'flex items-center gap-2 text-base/6 font-semibold', badge = 'rounded-md bg-secondary px-2 py-0.5 text-xs font-medium text-muted-foreground tabular-nums'

/** What this actor may record on a pending assessment, mirroring DecideImpact: the consumer adopts a confirmed
 *  replacement; an independent PM or discipline lead records Unaffected. */
function impactActions(row: Detail, o: CoordOptions, i: Detail['impacts'][number]): ('Adopt' | 'Unaffected')[] {
  const use = row.uses.find(u => u.id === i.basisUseId)
  if (i.status !== 'Pending Assessment' || !use?.isCurrent || !workRef(o, use.targetType, use.targetId)) return []
  return [...(i.newVersionId && o.actorId === use.ownerId ? ['Adopt' as const] : []),
    ...(row.canManage && o.actorId !== use.ownerId ? ['Unaffected' as const] : [])]
}

export function DesignBasisTab() {
  const project = useCurrentProject(), qc = useQueryClient(), [sp, setSp] = useSearchParams(), [adding, setAdding] = useState(false)
  const selected = sp.get('basis'), page = Math.max(1, Number(sp.get('page')) || 1)
  const kind = sp.get('kind') ?? '', status = sp.get('status') ?? '', discipline = sp.get('discipline') ?? ''
  const scope = sp.get('scope') ?? '', overdue = sp.get('overdue') ?? '', affectedWorkId = sp.get('affectedWorkId') ?? ''
  const set = (name: string, value: string) => setSp(p => { const next = new URLSearchParams(p); if (value) next.set(name, value); else next.delete(name);
    if (name !== 'basis' && name !== 'page') next.delete('page'); return next })
  const base = `projects/${project.id}/design-basis`
  const options = useQuery({ queryKey: ['coord-options', project.id], queryFn: () => get<CoordOptions>(`projects/${project.id}/changes/options`) })
  const team = useQuery({ queryKey: ['p', project.id, 'team'], queryFn: () => get<Team>(`projects/${project.id}/team`) })
  const memberDisciplineId = team.data?.members.find(m => m.userId === options.data?.actorId)?.primaryDisciplineId
  const canAssign = project.permissions.isPm || project.permissions.leadOf.length > 0
  const allowedDisciplines = project.permissions.isPm ? project.disciplines.map(d => d.id)
    : project.permissions.leadOf.length ? project.permissions.leadOf
      : memberDisciplineId && project.myRoles.includes('TeamMember') ? [memberDisciplineId] : []
  const canCreate = !!options.data?.canWrite && allowedDisciplines.length > 0
  const filters = { kind, status, disciplineId: discipline, scope, overdue, affectedWorkId }
  const params = new URLSearchParams({ page: String(page) })
  for (const [key, value] of Object.entries(filters)) if (value) params.set(key, value)
  const list = useQuery({ queryKey: ['design-basis', project.id, page, filters],
    queryFn: () => get<{ items: EntryRow[]; pageSize: number; totalCount: number }>(`${base}?${params}`) })
  const refresh = () => { qc.invalidateQueries({ queryKey: ['design-basis', project.id] }); qc.invalidateQueries({ queryKey: ['design-basis-detail', project.id] }) }
  const member = (id?: string) => team.data?.members.find(m => m.userId === id)?.displayName
  const name = (id?: string) => member(id) ?? t('coord.unavailable')
  const work = options.data && affectedWorkId ? [...options.data.tasks, ...options.data.deliverables].find(w => w.id === affectedWorkId) : undefined
  // Active filters as removable tokens (§13.0 Filters); the URL keeps the same parameters.
  const tokens = [
    kind && { key: 'kind', label: t('basis.kind'), value: kind === 'Assumption' ? t('basis.assumption') : kind === 'Criterion' ? t('basis.criterion') : kind },
    status && { key: 'status', label: t('basis.status'), value: status },
    discipline && { key: 'discipline', label: t('basis.discipline'), value: project.disciplines.find(d => d.id === discipline)?.name ?? t('common.dash') },
    scope && { key: 'scope', label: t('basis.scope'), value: scope },
    affectedWorkId && { key: 'affectedWorkId', label: t('basis.affectedWork'), value: work ? `${work.key} · ${work.name}` : t('common.dash') },
    overdue && { key: 'overdue', label: t('basis.overdueOnly'), value: t('common.yes') },
  ].filter(x => !!x)
  const clear = () => setSp(p => { const next = new URLSearchParams(p); for (const k of ['kind', 'status', 'discipline', 'scope', 'overdue', 'affectedWorkId', 'page']) next.delete(k); return next })
  const discName = (id: string) => project.disciplines.find(d => d.id === id)?.name
  return <Page title={t('basis.title')} subtitle={t('basis.subtitle')}
    actions={<><ViewMenu listType="design-basis" projectId={project.id} panelParam="basis" /><ExportMenu path={`${base}/export`} params={filters} name={`${project.projectNumber}-design-basis`} />
      {canCreate && <Button onClick={() => setAdding(true)}><Plus className="size-4" />{t('basis.new')}</Button>}</>}>
    {options.error && <ErrorBanner error={options.error} retry={() => options.refetch()} />}
    <FilterBar>
      <div className="flex flex-wrap items-end gap-3">
        <div className="w-full sm:w-40"><SelectField label={t('basis.kind')} value={kind} onChange={v => set('kind', v)} required={false}
          choices={[{ value: 'Criterion', label: t('basis.criterion') }, { value: 'Assumption', label: t('basis.assumption') }]} /></div>
        <div className="w-full sm:w-40"><SelectField label={t('basis.status')} value={status} onChange={v => set('status', v)} required={false}
          choices={['Proposed', 'Confirmed', 'Superseded', 'Withdrawn'].map(value => ({ value, label: value }))} /></div>
        <div className="w-full sm:w-52"><SelectField label={t('basis.discipline')} value={discipline} onChange={v => set('discipline', v)} required={false}
          choices={project.disciplines.map(d => ({ value: d.id, label: d.name }))} /></div>
        <Field label={t('basis.scope')} htmlFor="basis-scope-filter" className="w-full sm:w-56"><Input id="basis-scope-filter" type="search" value={scope} onChange={e => set('scope', e.target.value)} /></Field>
        {options.data && <div className="w-full sm:w-64"><SelectField label={t('basis.affectedWork')} value={affectedWorkId} onChange={v => set('affectedWorkId', v)}
          required={false} choices={[...options.data.tasks, ...options.data.deliverables].map(w => ({ value: w.id, label: `${w.key} · ${w.name}` }))} /></div>}
      </div>
      <div className="flex flex-wrap items-center gap-2"><ChipToggle on={overdue === 'true'} onClick={() => set('overdue', overdue === 'true' ? '' : 'true')}>{t('basis.overdueOnly')}</ChipToggle></div>
      <ActiveFilters tokens={tokens} onRemove={k => set(k, '')} onClear={clear} />
    </FilterBar>
    {list.isPending ? <div className="rounded-lg border bg-card"><Loading rows={4} /></div> : list.error ? <ErrorBanner error={list.error} retry={() => list.refetch()} /> : !list.data.items.length ?
      <div className="rounded-lg border bg-card"><Empty title={t(tokens.length ? 'register.noMatch' : 'basis.empty')}>{t('basis.emptyHint')}</Empty></div> :
      <TableRegion><table className="w-full text-left text-sm"><caption className="sr-only">{t('basis.title')}</caption>
        <thead className="bg-muted"><tr>{[t('coord.item'), t('basis.kind'), t('basis.discipline'), t('basis.owner'), t('basis.current'), t('basis.pending')].map(h => <th key={h} scope="col" className={thCls}>{h}</th>)}<th scope="col" className={cn(thCls, 'text-right')}>{t('basis.conflicts')}</th></tr></thead>
        <tbody>{list.data.items.map(row => <tr key={row.id} className={cn('border-t hover:bg-muted', selected === row.id && 'bg-accent shadow-[inset_3px_0_0_var(--primary)] hover:bg-accent')}>
          <td className={cn(tdCls, 'min-w-64')}><button className="break-words text-left font-semibold text-primary underline-offset-4 hover:underline" aria-current={selected === row.id || undefined} onClick={() => set('basis', row.id)}><span className="key font-normal">{row.key}</span> · {row.title}</button></td>
          <td className={tdCls}><Kind kind={row.kind} /></td>
          <td className={tdCls}>{discName(row.projectDisciplineId) ?? <span className="text-muted-foreground">{t('coord.unavailable')}</span>}</td>
          <td className={tdCls}><PersonLabel id={row.ownerId} name={member(row.ownerId)} /></td>
          <td className={tdCls}>{row.currentStatus ? <CoordStatus status={row.currentStatus} /> : <Pill tone="idle">{t('basis.notConfirmed')}</Pill>}</td>
          <td className={tdCls}>{row.latestStatus === 'Proposed' ? <CoordStatus status={row.latestStatus} /> : <span className="text-muted-foreground">{t('common.dash')}</span>}</td>
          <td className={cn(tdCls, 'text-right tabular-nums')}><Count n={row.conflictCount} tone="warn" /></td></tr>)}</tbody>
      </table></TableRegion>}
    {list.data && <RegisterPager label={t('basis.pager')} page={page} total={list.data.totalCount} pageSize={list.data.pageSize} onPage={n => set('page', String(n))} />}
    {adding && options.data && <BasisForm base={base} number={project.projectNumber} options={options.data}
      allowedDisciplines={allowedDisciplines} canAssign={canAssign} onClose={() => setAdding(false)} onDone={id => { setAdding(false); refresh(); set('basis', id) }} />}
    {selected && <BasisDetail base={base} id={selected} number={project.projectNumber} options={options.data} name={name}
      close={() => set('basis', '')} refresh={refresh} />}
  </Page>
}

function VersionFields({ draft, setDraft }: { draft: VersionDraft; setDraft: (v: VersionDraft) => void }) {
  const update = (key: keyof VersionDraft, value: string) => setDraft({ ...draft, [key]: value })
  return <div className="space-y-4">
    <Field label={t('basis.scope')} htmlFor="basis-scope"><Input id="basis-scope" required maxLength={500} value={draft.scope} onChange={e => update('scope', e.target.value)} /></Field>
    <Field label={t('basis.statement')} htmlFor="basis-statement"><Textarea id="basis-statement" required maxLength={4000} value={draft.statement} onChange={e => update('statement', e.target.value)} /></Field>
    <div className="grid gap-3 sm:grid-cols-2"><Field label={t('basis.numeric')} htmlFor="basis-numeric"><Input id="basis-numeric" type="number" step="any" value={draft.numericValue} onChange={e => update('numericValue', e.target.value)} /></Field>
      <Field label={t('basis.units')} htmlFor="basis-units"><Input id="basis-units" value={draft.units} onChange={e => update('units', e.target.value)} /></Field></div>
    <div className="grid gap-3 sm:grid-cols-2"><Field label={t('basis.sourceSystem')} htmlFor="basis-source-system"><Input id="basis-source-system" value={draft.sourceSystem} onChange={e => update('sourceSystem', e.target.value)} /></Field>
      <Field label={t('basis.sourceId')} htmlFor="basis-source-id"><Input id="basis-source-id" value={draft.stableSourceId} onChange={e => update('stableSourceId', e.target.value)} /></Field></div>
    <Field label={t('basis.sourceUrl')} htmlFor="basis-source-url"><Input id="basis-source-url" type="url" value={draft.sourceUrl} onChange={e => update('sourceUrl', e.target.value)} /></Field>
    <div className="grid gap-3 sm:grid-cols-2"><Field label={t('basis.revision')} htmlFor="basis-revision"><Input id="basis-revision" value={draft.declaredRevision} onChange={e => update('declaredRevision', e.target.value)} /></Field>
      <Field label={t('basis.due')} htmlFor="basis-due"><Input id="basis-due" type="date" required value={draft.confirmationDueDate} onChange={e => update('confirmationDueDate', e.target.value)} /></Field></div>
    <p className="text-xs/[18px] text-muted-foreground">{t('basis.manual')}</p>
  </div>
}

function BasisForm({ base, number, options, onClose, onDone, existing, editing, allowedDisciplines, canAssign = true }: { base: string; number: string; options: CoordOptions;
  onClose: () => void; onDone: (id: string) => void; existing?: Detail; editing?: Version; allowedDisciplines?: string[]; canAssign?: boolean }) {
  const current = editing ?? existing?.versions.find(v => v.version.id === existing.entry.currentVersionId)?.version
  const [kind, setKind] = useState(existing?.entry.kind ?? 'Assumption'), [title, setTitle] = useState(existing?.entry.title ?? '')
  const [ownerId, setOwnerId] = useState(existing?.entry.ownerId ?? options.actorId)
  const [disciplineId, setDisciplineId] = useState(existing?.entry.projectDisciplineId ?? (allowedDisciplines?.length === 1 ? allowedDisciplines[0] : ''))
  const [approverId, setApproverId] = useState(existing?.entry.independentApproverId ?? '')
  const [draft, setDraft] = useState<VersionDraft>(() => fromVersion(current))
  const [reason, setReason] = useState(''), [busy, setBusy] = useState(false), [error, setError] = useState<unknown>(null)
  const [duplicateId, setDuplicateId] = useState(''), [inspected, setInspected] = useState(false)
  const decisions = useQuery({ queryKey: ['basis-decision-options', base],
    queryFn: () => get<{ id: string; key: string; subject: string; status: string }[]>(base.replace(/\/design-basis$/, '/decisions')) })
  const decisionChoices = decisions.data?.map(d => ({ value: d.id, label: `${d.key} · ${d.subject} · ${tv(d.status)}` })) ?? []
  if (draft.decisionId && !decisionChoices.some(d => d.value === draft.decisionId))
    decisionChoices.push({ value: draft.decisionId, label: t('coord.unavailable') })
  const receipt = useRef<{ signature: string; requestId: string } | null>(null)
  const submit = async (event: React.FormEvent) => {
    event.preventDefault(); setError(null); setBusy(true)
    const body = existing ? { entryRowVersion: existing.entry.rowVersion,
      ...(editing ? { versionRowVersion: editing.rowVersion } : { currentVersionRowVersion: current!.rowVersion }),
      version: payloadVersion(draft), reason, inspectedDuplicateId: inspected ? duplicateId : null } : { kind, title, ownerId, projectDisciplineId: disciplineId,
      independentApproverId: approverId || null, version: payloadVersion(draft), reason: null,
      inspectedDuplicateId: inspected ? duplicateId : null }
    const signature = JSON.stringify(body)
    if (!receipt.current || receipt.current.signature !== signature) receipt.current = { signature, requestId: crypto.randomUUID() }
    try { const result = await post<{ id: string }>(existing ? editing ? `${base}/${existing.entry.id}/versions/${editing.id}/edit` : `${base}/${existing.entry.id}/propose` : base,
      { ...body, requestId: receipt.current.requestId })
      toast.success(t('coord.saved')); onDone(existing?.entry.id ?? result.id)
    } catch (e) { setError(e); if (e instanceof ApiError && e.code === 'basis_duplicate' && typeof e.body.existingId === 'string') {
      setDuplicateId(e.body.existingId); setInspected(false) } } finally { setBusy(false) }
  }
  return <Dialog open onOpenChange={o => !o && !busy && onClose()}><DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
    <DialogHeader><DialogTitle>{t(editing ? 'basis.editProposed' : existing ? 'basis.propose' : 'basis.new')}</DialogTitle><DialogDescription>{t('basis.subtitle')}</DialogDescription></DialogHeader>
    <form onSubmit={submit} className="space-y-4"><fieldset disabled={busy} className="space-y-4">
      {!existing && <><SelectField label={t('basis.kind')} value={kind} onChange={setKind} choices={[{ value: 'Criterion', label: t('basis.criterion') }, { value: 'Assumption', label: t('basis.assumption') }]} />
        <Field label={t('coord.title')} htmlFor="basis-title"><Input id="basis-title" required maxLength={200} value={title} onChange={e => setTitle(e.target.value)} /></Field>
        {canAssign && <SelectField label={t('basis.owner')} value={ownerId} onChange={setOwnerId} choices={options.people.map(p => ({ value: p.id, label: p.displayName }))} />}
        <SelectField label={t('basis.discipline')} value={disciplineId} onChange={setDisciplineId}
          choices={options.disciplines.filter(d => !allowedDisciplines || allowedDisciplines.includes(d.id)).map(d => ({ value: d.id, label: d.name }))} />
        {canAssign && <SelectField label={t('basis.approver')} value={approverId} onChange={setApproverId} required={false} choices={options.people.map(p => ({ value: p.id, label: p.displayName }))} />}</>}
      <VersionFields draft={draft} setDraft={setDraft} />
      {decisions.error && <ErrorBanner error={decisions.error} retry={() => decisions.refetch()} />}
      <SelectField label={t('basis.sourceDecision')} value={draft.decisionId}
        onChange={decisionId => setDraft(v => ({ ...v, decisionId }))} required={false} choices={decisionChoices} />
      {existing && <Field label={t('basis.reason')} htmlFor="basis-reason"><Textarea id="basis-reason" required minLength={5} value={reason} onChange={e => setReason(e.target.value)} /></Field>}
      {duplicateId && <div className="space-y-2 rounded-md border border-warn/40 bg-warn-bg px-4 py-3 text-sm"><p className="flex gap-2 text-warn"><span aria-hidden>▲</span><span>{t('basis.duplicate')}</span></p>
        <Link className="text-primary underline underline-offset-4" to={`/projects/${number}/design-basis?basis=${duplicateId}`} target="_blank" rel="noopener noreferrer">{t('basis.openExisting')}</Link>
        <label className="flex items-start gap-2"><input type="checkbox" className="mt-0.5 size-4 shrink-0 accent-(--primary)" checked={inspected} onChange={e => setInspected(e.target.checked)} />{t('basis.inspected')}</label></div>}
    </fieldset>{error != null && <ErrorBanner error={error} />}
      <DialogFooter><Button type="button" variant="outline" onClick={onClose}>{t('common.cancel')}</Button><Button disabled={busy || !!duplicateId && !inspected}>{busy && <Spinner />}{busy ? t('common.saving') : t('common.save')}</Button></DialogFooter>
    </form></DialogContent></Dialog>
}

function BasisDetail({ base, id, number, options, name, close, refresh }: { base: string; id: string; number: string;
  options?: CoordOptions; name: (id?: string) => string; close: () => void; refresh: () => void }) {
  const q = useQuery({ queryKey: ['design-basis-detail', base, id], queryFn: () => get<Detail>(`${base}/${id}`) })
  const [action, setAction] = useState<'assign' | 'propose' | 'edit' | 'confirm' | 'proceed' | 'use' | 'withdraw' | 'resolveConflict' | null>(null)
  const [selectedImpact, setSelectedImpact] = useState<string | null>(null)
  const [selectedVersion, setSelectedVersion] = useState<Version | null>(null)
  const [selectedConflict, setSelectedConflict] = useState<string | null>(null)
  const row = q.data, current = row?.versions.find(v => v.version.id === row.entry.currentVersionId)?.version
  const proposed = row?.versions.find(v => v.version.status === 'Proposed')?.version
  const done = () => { setAction(null); setSelectedImpact(null); refresh(); q.refetch() }
  if (action === 'propose' && row && options) return <BasisForm base={base} number={number} options={options} existing={row}
    onClose={() => setAction(null)} onDone={done} />
  if (action === 'edit' && row && options && selectedVersion) return <BasisForm base={base} number={number} options={options}
    existing={row} editing={selectedVersion} onClose={() => setAction(null)} onDone={done} />
  const unresolved = row?.conflicts.filter(c => !c.resolved) ?? []
  return <><Dialog open onOpenChange={o => !o && close()}><DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-3xl">
    <DialogHeader><DialogTitle>{row ? `${row.entry.key} · ${row.entry.title}` : t('basis.title')}</DialogTitle>
      <DialogDescription>{t('basis.subtitle')}</DialogDescription></DialogHeader>
    {q.isPending ? <Loading rows={4} /> : q.error ? <ErrorBanner error={q.error} retry={() => q.refetch()} /> : row &&
      <div className="space-y-6 text-sm">
        <div className="flex flex-wrap items-center gap-x-3 gap-y-2"><Kind kind={row.entry.kind} />
          <span><span className="text-muted-foreground">{t('basis.owner')}:</span> {name(row.entry.ownerId)}</span>
          <span><span className="text-muted-foreground">{t('basis.discipline')}:</span> {options?.disciplines.find(d => d.id === row.entry.projectDisciplineId)?.name ?? t('coord.unavailable')}</span></div>
        <div className="flex flex-wrap gap-2">{row.canConfirm && proposed && <Button size="sm" onClick={() => setAction('confirm')}>{t('basis.confirm')}</Button>}
          {row.canManage && current && !proposed && options && <Button size="sm" variant="outline" onClick={() => setAction('propose')}>{t('basis.propose')}</Button>}
          {row.canManage && (current || proposed) && options && <Button size="sm" variant="outline" onClick={() => setAction('assign')}>{t('basis.owner')}</Button>}
          {row.canManage && row.entry.kind === 'Assumption' && proposed && <Button size="sm" variant="outline" onClick={() => setAction('proceed')}>{t('basis.proceed')}</Button>}
          {options?.canWrite && [...options.tasks, ...options.deliverables].some(w => w.ownerId === options.actorId) && (current || proposed) &&
            <Button size="sm" variant="outline" onClick={() => setAction('use')}>{t('basis.linkUse')}</Button>}</div>
        <section className="space-y-3"><h3 className={subhead}>{t('basis.version')}</h3>{row.versions.map(({ version: v, sourceMissing }) => {
          const canEdit = row.canEditProposed && v.status === 'Proposed' && !row.uses.some(u => u.versionId === v.id) && !row.dispositions.some(d => d.versionId === v.id)
          const canWithdraw = row.canManage && (v.status === 'Proposed' || v.status === 'Confirmed')
          return <div key={v.id} className="space-y-2 rounded-lg border p-4"><div className="flex flex-wrap items-center gap-2"><span className="font-semibold">{t('basis.version')} {v.number}</span><CoordStatus status={v.status} />{row.entry.currentVersionId === v.id && <span className={badge}>{t('basis.current')}</span>}</div>
            <p>{t('basis.scope')}: {v.scope}</p><p className="whitespace-pre-wrap text-base/6">{v.statement}{v.numericValue != null && <span className="tabular-nums"> · {v.numericValue} {v.units ?? ''}</span>}</p>
            {sourceMissing && <Warn>{t('basis.missingSource')}</Warn>}
            <p className="text-xs/[18px] text-muted-foreground">{t('basis.manual')}{v.sourceSystem && ` · ${v.sourceSystem}`}{v.stableSourceId && ` · ${v.stableSourceId}`}{v.declaredRevision && ` · ${t('basis.revision')}: ${v.declaredRevision}`}</p>
            {v.sourceUrl && <a className="text-primary underline underline-offset-4" href={v.sourceUrl} target="_blank" rel="noopener noreferrer">{t('basis.sourceUrl')}</a>}
            {v.decisionId && <p><Link className="text-primary underline underline-offset-4"
              to={`/projects/${number}/decisions?panel=Decision:${v.decisionId}`}>{t('basis.sourceDecision')}</Link></p>}
            {v.confirmationDueDate ? <p>{t('basis.due')}: <span className="tabular-nums">{fmtDate(v.confirmationDueDate)}</span></p>
              : v.status === 'Proposed' && <Warn>{t('basis.dueRequired')}</Warn>}
            {v.confirmedAt && <p>{t('basis.confirm')}: {name(v.confirmedBy)} · <span className="tabular-nums">{fmtDate(v.confirmedAt)}</span> · {v.confirmationRationale}</p>}
            {(canEdit || canWithdraw) && <div className="flex flex-wrap gap-2 pt-1">
              {canEdit && <Button size="sm" variant="outline" onClick={() => { setSelectedVersion(v); setAction('edit') }}>{t('basis.editProposed')}</Button>}
              {canWithdraw && <Button size="sm" variant="ghost" className="text-bad hover:text-bad" onClick={() => { setSelectedVersion(v); setAction('withdraw') }}>{t('basis.withdraw')}</Button>}
            </div>}
          </div>
        })}</section>
        <section className="space-y-3"><h3 className={subhead}>{t('basis.conflicts')} <span className={badge}>{unresolved.length}</span></h3>
          {!unresolved.length && <p className="text-muted-foreground">{t('basis.noConflict')}</p>}
          {unresolved.map(c => <div key={c.id} className="space-y-1 rounded-lg border border-warn/40 bg-warn-bg p-4">
            <p><span className="key">{c.left.entryKey}</span>: {c.left.numericValue ?? c.left.statement} {c.left.units ?? ''}</p>
            <p><span className="key">{c.right.entryKey}</span>: {c.right.numericValue ?? c.right.statement} {c.right.units ?? ''}</p>
            <p>{t('basis.scope')}: {c.left.scope}</p>
            {row.canManage && row.versions.some(v => v.version.id === c.left.versionId || v.version.id === c.right.versionId) && <Button size="sm" variant="outline" className="mt-1" onClick={() => { setSelectedConflict(c.id); setAction('resolveConflict') }}>{t('basis.resolveConflict')}</Button>}
          </div>)}</section>
        <section className="space-y-2"><h3 className={subhead}>{t('basis.uses')} <span className={badge}>{row.uses.length}</span></h3><ul className="divide-y rounded-lg border">{row.uses.map(u =>
          <li key={u.id} className="px-4 py-3">{options ? <WorkLink options={options} type={u.targetType} id={u.targetId} number={number} /> :
            <Link className="text-primary underline underline-offset-4" to={`/projects/${number}/${u.targetType === 'Task' ? 'tasks' : 'deliverables'}?panel=${u.targetType}:${u.targetId}`}>{u.targetType}</Link>}
            {' · '}{t('basis.version')} {row.versions.find(v => v.version.id === u.versionId)?.version.number ?? '?'} · {u.isCurrent ? t('basis.currentUse') : t('basis.historicalUse')}
            {u.isCurrent && staleUse(row.versions.find(v => v.version.id === u.versionId)?.version.status)} · {u.intendedUse}</li>)}</ul></section>
        <section className="space-y-2"><h3 className={subhead}>{t('basis.impacts')} <span className={badge}>{row.impacts.length}</span></h3><ul className="space-y-2">{row.impacts.map(i =>
          <li key={i.id} className="space-y-2 rounded-lg border p-4"><div className="flex flex-wrap items-center gap-2"><CoordStatus status={i.status} /><span>{name(i.ownerId)}</span><span className="text-muted-foreground">· {t('basis.version')} {row.versions.find(v => v.version.id === i.oldVersionId)?.version.number} → {i.newVersionId ? row.versions.find(v => v.version.id === i.newVersionId)?.version.number : i.withdrawalVersionId ? t('basis.withdrawn') : t('basis.decisionReopened')}</span></div>
            {i.rationale && <p>{t('basis.reason')}: {i.rationale}</p>}{i.evidenceUrl && <a className="text-primary underline underline-offset-4" href={i.evidenceUrl} target="_blank" rel="noopener noreferrer">{t('basis.evidence')}</a>}
            {options && impactActions(row, options, i).length > 0 &&
              <div><Button size="sm" variant="outline" onClick={() => setSelectedImpact(i.id)}>{t('basis.decideImpact')}</Button></div>}</li>)}</ul></section>
        {row.dispositions.length > 0 && <section className="space-y-2"><h3 className={subhead}>{t('basis.proceed')}</h3><ul className="divide-y rounded-lg border">{row.dispositions.map(d =>
          <li key={d.id} className="px-4 py-3">{d.scope} · {name(d.ownerId)} · {t('basis.expiry')}: <span className="tabular-nums">{fmtDate(d.expiresOn)}</span> · {d.reason}</li>)}</ul></section>}
      </div>}
  </DialogContent></Dialog>
    {row && action === 'assign' && options && <AssignForm base={base} entry={row.entry} options={options}
      close={() => setAction(null)} done={done} />}
    {row && action === 'confirm' && proposed && <ConfirmForm base={base} id={id} entry={row.entry} version={proposed}
      close={() => setAction(null)} done={done} />}
    {row && action === 'proceed' && proposed && options && <ProceedForm base={base} id={id} version={proposed}
      options={options} close={() => setAction(null)} done={done} />}
    {row && action === 'use' && options && <UseForm base={base} id={id} versions={row.versions.map(v => v.version)}
      currentId={current?.id} options={options} close={() => setAction(null)} done={done} />}
    {row && action === 'withdraw' && selectedVersion && <WithdrawForm base={base} id={id} entry={row.entry} version={selectedVersion}
      close={() => { setAction(null); setSelectedVersion(null) }} done={done} />}
    {row && action === 'resolveConflict' && selectedConflict && <ConflictForm base={base} conflict={row.conflicts.find(c => c.id === selectedConflict)!} entry={row.entry}
      versions={row.versions.map(v => v.version)} close={() => { setAction(null); setSelectedConflict(null) }} done={done} />}
    {row && selectedImpact && options && <ImpactForm base={base} id={id} number={number} row={row} impactId={selectedImpact}
      options={options} close={() => setSelectedImpact(null)} done={done} />}
  </>
}

function AssignForm({ base, entry, options, close, done }: { base: string; entry: Entry; options: CoordOptions;
  close: () => void; done: () => void }) {
  const [ownerId, setOwnerId] = useState(entry.ownerId)
  const [approverId, setApproverId] = useState(entry.independentApproverId ?? '')
  const [reason, setReason] = useState('')
  return <CommandForm path={`${base}/${entry.id}/assign`} title={t('basis.owner')} onClose={close} onDone={done}
    payload={() => ({ entryRowVersion: entry.rowVersion, ownerId, independentApproverId: approverId || null, reason })}>
    <SelectField label={t('basis.owner')} value={ownerId} onChange={setOwnerId}
      choices={options.people.map(p => ({ value: p.id, label: p.displayName }))} />
    <SelectField label={t('basis.approver')} value={approverId} onChange={setApproverId} required={false}
      choices={options.people.map(p => ({ value: p.id, label: p.displayName }))} />
    <Field label={t('basis.reason')} htmlFor="basis-assign-reason"><Textarea id="basis-assign-reason" required minLength={5}
      value={reason} onChange={e => setReason(e.target.value)} /></Field>
  </CommandForm>
}

function WithdrawForm({ base, id, entry, version, close, done }: { base: string; id: string; entry: Entry; version: Version; close: () => void; done: () => void }) {
  const [reason, setReason] = useState('')
  return <CommandForm path={`${base}/${id}/versions/${version.id}/withdraw`} title={t('basis.withdraw')} hint={t('basis.withdrawHint')} onClose={close} onDone={done}
    payload={() => ({ entryRowVersion: entry.rowVersion, versionRowVersion: version.rowVersion, reason })}>
    <p className="flex flex-wrap items-center gap-2"><span className="font-semibold">{t('basis.version')} {version.number}</span><CoordStatus status={version.status} /></p>
    <Field label={t('basis.reason')} htmlFor="basis-withdraw-reason"><Textarea id="basis-withdraw-reason" required minLength={5} value={reason} onChange={e => setReason(e.target.value)} /></Field>
  </CommandForm>
}

function ConflictForm({ base, conflict, entry, versions, close, done }: { base: string; conflict: Detail['conflicts'][number]; entry: Entry; versions: Version[]; close: () => void; done: () => void }) {
  const candidates = versions.filter(v => v.status === 'Confirmed' &&
    v.id !== conflict.left.versionId && v.id !== conflict.right.versionId &&
    (v.supersedesVersionId === conflict.left.versionId || v.supersedesVersionId === conflict.right.versionId))
  const [resolutionVersionId, setResolutionVersionId] = useState(candidates[0]?.id ?? '')
  const [rationale, setRationale] = useState('')
  const resolution = candidates.find(v => v.id === resolutionVersionId)
  return <CommandForm path={`${base}/conflicts/${conflict.id}/resolve`} title={t('basis.resolveConflict')} hint={t('basis.resolveConflictHint')} onClose={close} onDone={done}
    payload={() => ({ conflictRowVersion: conflict.rowVersion, resolutionVersionId, resolutionVersionRowVersion: resolution?.rowVersion, rationale })}>
    <SelectField label={t('basis.resolutionVersion')} value={resolutionVersionId} onChange={setResolutionVersionId}
      choices={candidates.map(v => ({ value: v.id, label: `${t('basis.version')} ${v.number}` }))} />
    <p className="text-xs/[18px] text-muted-foreground">{entry.title}</p>
    <Field label={t('basis.reason')} htmlFor="basis-conflict-reason"><Textarea id="basis-conflict-reason" required minLength={5} value={rationale} onChange={e => setRationale(e.target.value)} /></Field>
  </CommandForm>
}

function ImpactForm({ base, id, number, row, impactId, options, close, done }: { base: string; id: string; number: string; row: Detail;
  impactId: string; options: CoordOptions; close: () => void; done: () => void }) {
  const impact = row.impacts.find(i => i.id === impactId)!, use = row.uses.find(u => u.id === impact.basisUseId)!
  const next = impact.newVersionId ? row.versions.find(v => v.version.id === impact.newVersionId)?.version : undefined
  const target = workRef(options, use.targetType, use.targetId), actions = impactActions(row, options, impact)
  const [decision, setDecision] = useState<string>(actions[0] ?? '')
  const [rationale, setRationale] = useState(''), [evidenceUrl, setEvidenceUrl] = useState('')
  return <CommandForm path={`${base}/${id}/impacts/${impactId}/decide`} title={t('basis.decideImpact')}
    hint={t('basis.impactHint')} onClose={close} onDone={done}
    payload={() => ({ assessmentRowVersion: impact.rowVersion, basisUseRowVersion: use.rowVersion,
      newVersionRowVersion: next?.rowVersion, targetRowVersion: target?.rowVersion, action: decision, rationale, evidenceUrl })}>
    <div className="space-y-1 rounded-md bg-muted px-4 py-3 text-sm"><p className="font-medium">{t('basis.version')} {row.versions.find(v => v.version.id === impact.oldVersionId)?.version.number} → {next ? next.number : impact.withdrawalVersionId ? t('basis.withdrawn') : t('basis.decisionReopened')}</p>
      {target ? <WorkLink options={options} type={use.targetType} id={use.targetId} number={number} /> : <p className="text-muted-foreground">{t('coord.unavailable')}</p>}</div>
    <SelectField label={t('basis.decision')} value={decision} onChange={setDecision}
      choices={actions.map(a => ({ value: a, label: t(a === 'Adopt' ? 'basis.adopt' : 'basis.unaffected') }))} />
    <Field label={t('basis.reason')} htmlFor="basis-impact-reason"><Textarea id="basis-impact-reason" required minLength={5} value={rationale} onChange={e => setRationale(e.target.value)} /></Field>
    <Field label={t('basis.evidence')} htmlFor="basis-impact-evidence"><Input id="basis-impact-evidence" type="url" required value={evidenceUrl} onChange={e => setEvidenceUrl(e.target.value)} /></Field>
    {!target && <p role="status" className="flex gap-2 rounded-md border border-warn/40 bg-warn-bg px-4 py-3 text-sm text-warn"><span aria-hidden>▲</span><span>{t('basis.targetUnavailable')}</span></p>}
  </CommandForm>
}

function ProceedForm({ base, id, version, options, close, done }: { base: string; id: string; version: Version;
  options: CoordOptions; close: () => void; done: () => void }) {
  const [ownerId, setOwnerId] = useState(''), [expiry, setExpiry] = useState(''), [reason, setReason] = useState('')
  return <CommandForm path={`${base}/${id}/versions/${version.id}/proceed`} title={t('basis.proceed')}
    hint={t('basis.proceedHint')} onClose={close} onDone={done}
    payload={() => ({ versionRowVersion: version.rowVersion, scope: version.scope, ownerId, expiresOn: expiry, reason })}>
    <p className="rounded-md bg-muted px-4 py-3 text-sm">{t('basis.scope')}: {version.scope}</p><SelectField label={t('basis.owner')} value={ownerId} onChange={setOwnerId}
      choices={options.people.map(p => ({ value: p.id, label: p.displayName }))} />
    <Field label={t('basis.expiry')} htmlFor="basis-expiry"><Input id="basis-expiry" type="date" required value={expiry} onChange={e => setExpiry(e.target.value)} /></Field>
    <Field label={t('basis.reason')} htmlFor="basis-proceed-reason"><Textarea id="basis-proceed-reason" required minLength={5} value={reason} onChange={e => setReason(e.target.value)} /></Field>
  </CommandForm>
}

function UseForm({ base, id, versions, currentId, options, close, done }: { base: string; id: string;
  versions: Version[]; currentId?: string; options: CoordOptions; close: () => void; done: () => void }) {
  const [versionId, setVersionId] = useState(currentId ?? versions.find(v => v.status === 'Proposed')?.id ?? '')
  const [target, setTarget] = useState(''), [intendedUse, setIntendedUse] = useState('')
  const [targetType, targetId] = target.split(':')
  return <CommandForm path={`${base}/${id}/uses`} title={t('basis.linkUse')} onClose={close} onDone={done}
    payload={() => ({ versionId, targetType, targetId, intendedUse })}>
    <SelectField label={t('basis.version')} value={versionId} onChange={setVersionId}
      choices={versions.filter(v => v.id === currentId || v.status === 'Proposed').map(v => ({ value: v.id, label: `${t('basis.version')} ${v.number} · ${tv(v.status)}` }))} />
    <SelectField label={t('basis.target')} value={target} onChange={setTarget}
      choices={workChoices({ ...options, tasks: options.tasks.filter(w => w.ownerId === options.actorId),
        deliverables: options.deliverables.filter(w => w.ownerId === options.actorId) })} />
    <Field label={t('basis.intendedUse')} htmlFor="basis-use"><Textarea id="basis-use" required maxLength={2000} value={intendedUse} onChange={e => setIntendedUse(e.target.value)} /></Field>
  </CommandForm>
}

function ConfirmForm({ base, id, entry, version, close, done }: { base: string; id: string; entry: Entry;
  version: Version; close: () => void; done: () => void }) {
  const [rationale, setRationale] = useState('')
  return <CommandForm path={`${base}/${id}/versions/${version.id}/confirm`} title={t('basis.confirm')}
    hint={t('basis.confirmHint')} onClose={close} onDone={done}
    payload={() => ({ entryRowVersion: entry.rowVersion, versionRowVersion: version.rowVersion, rationale })}>
    <Field label={t('basis.reason')} htmlFor="basis-confirm-reason"><Textarea id="basis-confirm-reason" required minLength={5}
      value={rationale} onChange={e => setRationale(e.target.value)} /></Field>
  </CommandForm>
}
