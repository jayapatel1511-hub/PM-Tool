import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { ErrorBanner, Field, Loading, Page } from '@/components/hub/common'
import { ApiError, get, post } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { CommandForm, SelectField, type CoordOptions, workChoices, workRef, WorkLink } from './CoordinationForms'
import { useCurrentProject } from './ProjectLayout'

type EntryRow = { id: string; key: string; title: string; kind: string; ownerId: string; projectDisciplineId: string;
  currentVersionId?: string; rowVersion: number; currentStatus?: string; currentScope?: string; latestStatus?: string;
  latestVersionId?: string; confirmationDueDate?: string; conflictCount: number }
type Entry = EntryRow & { independentApproverId?: string }
type Version = { id: string; entryId: string; number: number; status: string; scope: string; statement: string;
  numericValue?: number; units?: string; sourceSystem?: string; stableSourceId?: string; sourceUrl?: string;
  declaredRevision?: string; confirmationDueDate?: string; rowVersion: number; supersedesVersionId?: string;
  confirmedBy?: string; confirmedAt?: string; confirmationRationale?: string }
type Detail = { entry: Entry; versions: { version: Version; sourceMissing: boolean }[];
  uses: { id: string; versionId: string; targetType: string; targetId: string; intendedUse: string; ownerId: string; rowVersion: number; isCurrent: boolean }[];
  impacts: { id: string; basisUseId: string; oldVersionId: string; newVersionId: string; status: string; ownerId: string; rowVersion: number; rationale?: string; evidenceUrl?: string }[];
  conflicts: { id: string; resolved: boolean; left: { versionId: string; entryKey: string; scope: string; statement: string; numericValue?: number; units?: string };
    right: { versionId: string; entryKey: string; scope: string; statement: string; numericValue?: number; units?: string } }[];
  dispositions: { id: string; versionId: string; scope: string; ownerId: string; approvedBy: string; expiresOn: string; reason: string }[];
  canManage: boolean; canConfirm: boolean }
type Team = { members: { userId: string; displayName: string }[] }
type VersionDraft = { scope: string; statement: string; numericValue: string; units: string; sourceSystem: string;
  stableSourceId: string; sourceUrl: string; declaredRevision: string; confirmationDueDate: string }
const blank: VersionDraft = { scope: '', statement: '', numericValue: '', units: '', sourceSystem: '', stableSourceId: '',
  sourceUrl: '', declaredRevision: '', confirmationDueDate: '' }
const fromVersion = (v?: Version): VersionDraft => v ? { scope: v.scope, statement: v.statement,
  numericValue: v.numericValue == null ? '' : String(v.numericValue), units: v.units ?? '', sourceSystem: v.sourceSystem ?? '',
  stableSourceId: v.stableSourceId ?? '', sourceUrl: v.sourceUrl ?? '', declaredRevision: v.declaredRevision ?? '',
  confirmationDueDate: v.confirmationDueDate ?? '' } : { ...blank }
const payloadVersion = (v: VersionDraft) => ({ scope: v.scope, statement: v.statement,
  numericValue: v.numericValue === '' ? null : Number(v.numericValue), units: v.units || null, sourceSystem: v.sourceSystem || null,
  stableSourceId: v.stableSourceId || null, sourceUrl: v.sourceUrl || null, declaredRevision: v.declaredRevision || null,
  confirmationDueDate: v.confirmationDueDate || null, decisionId: null })

export function DesignBasisTab() {
  const project = useCurrentProject(), qc = useQueryClient(), [sp, setSp] = useSearchParams(), [adding, setAdding] = useState(false)
  const selected = sp.get('basis'), page = Math.max(1, Number(sp.get('page')) || 1)
  const kind = sp.get('kind') ?? '', status = sp.get('status') ?? '', discipline = sp.get('discipline') ?? ''
  const set = (name: string, value: string) => setSp(p => { const next = new URLSearchParams(p); if (value) next.set(name, value); else next.delete(name);
    if (name !== 'basis') next.delete('page'); return next })
  const base = `projects/${project.id}/design-basis`
  const canCreate = project.permissions.isPm || project.permissions.leadOf.length > 0
  const options = useQuery({ queryKey: ['coord-options', project.id], queryFn: () => get<CoordOptions>(`projects/${project.id}/changes/options`) })
  const team = useQuery({ queryKey: ['p', project.id, 'team'], queryFn: () => get<Team>(`projects/${project.id}/team`) })
  const list = useQuery({ queryKey: ['design-basis', project.id, page, kind, status, discipline],
    queryFn: () => get<{ items: EntryRow[]; pageSize: number; totalCount: number }>(`${base}?page=${page}${kind ? `&kind=${encodeURIComponent(kind)}` : ''}${status ? `&status=${encodeURIComponent(status)}` : ''}${discipline ? `&disciplineId=${encodeURIComponent(discipline)}` : ''}`) })
  const refresh = () => { qc.invalidateQueries({ queryKey: ['design-basis', project.id] }); qc.invalidateQueries({ queryKey: ['design-basis-detail', project.id] }) }
  const name = (id?: string) => team.data?.members.find(m => m.userId === id)?.displayName ?? t('coord.unavailable')
  return <Page title={t('basis.title')} subtitle={t('basis.subtitle')}
    actions={canCreate && <Button size="sm" onClick={() => setAdding(true)}>{t('basis.new')}</Button>}>
    {options.error && <ErrorBanner error={options.error} retry={() => options.refetch()} />}
    <div className="flex flex-wrap gap-3 rounded border p-3">
      <SelectField label={t('basis.kind')} value={kind} onChange={v => set('kind', v)} required={false}
        choices={[{ value: 'Criterion', label: t('basis.criterion') }, { value: 'Assumption', label: t('basis.assumption') }]} />
      <SelectField label={t('basis.status')} value={status} onChange={v => set('status', v)} required={false}
        choices={['Proposed', 'Confirmed', 'Superseded', 'Withdrawn'].map(value => ({ value, label: value }))} />
      <SelectField label={t('basis.discipline')} value={discipline} onChange={v => set('discipline', v)} required={false}
        choices={project.disciplines.map(d => ({ value: d.id, label: d.name }))} />
    </div>
    {list.isPending ? <Loading rows={4} /> : list.error ? <ErrorBanner error={list.error} retry={() => list.refetch()} /> : !list.data.items.length ?
      <p className="rounded border p-8 text-center text-muted-foreground">{t('basis.empty')}</p> :
      <div className="overflow-x-auto rounded border"><table className="w-full text-left text-sm"><caption className="sr-only">{t('basis.title')}</caption>
        <thead className="bg-muted/60"><tr>{[t('coord.item'), t('basis.kind'), t('basis.discipline'), t('basis.owner'), t('basis.current'), t('basis.pending'), t('basis.conflicts')].map(h => <th key={h} scope="col" className="p-3">{h}</th>)}</tr></thead>
        <tbody>{list.data.items.map(row => <tr key={row.id} className="border-t"><td className="p-3"><button className="text-left font-medium text-primary underline" onClick={() => set('basis', row.id)}>{row.key} · {row.title}</button></td>
          <td className="p-3">{row.kind}</td><td className="p-3">{project.disciplines.find(d => d.id === row.projectDisciplineId)?.name ?? t('coord.unavailable')}</td>
          <td className="p-3">{name(row.ownerId)}</td><td className="p-3">{row.currentStatus ? tv(row.currentStatus) : t('basis.notConfirmed')}</td>
          <td className="p-3">{row.latestStatus === 'Proposed' ? tv(row.latestStatus) : '—'}</td><td className="p-3">{row.conflictCount}</td></tr>)}</tbody>
      </table></div>}
    {list.data && <div className="flex items-center justify-end gap-3"><Button size="sm" variant="outline" disabled={page <= 1} onClick={() => set('page', String(page - 1))}>{t('handoff.previous')}</Button>
      <span>{t('handoff.page', { n: page })}</span><Button size="sm" variant="outline" disabled={page * list.data.pageSize >= list.data.totalCount} onClick={() => set('page', String(page + 1))}>{t('handoff.next')}</Button></div>}
    {adding && options.data && <BasisForm base={base} number={project.projectNumber} options={options.data} onClose={() => setAdding(false)} onDone={id => { setAdding(false); refresh(); set('basis', id) }} />}
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
      <Field label={t('basis.due')} htmlFor="basis-due"><Input id="basis-due" type="date" value={draft.confirmationDueDate} onChange={e => update('confirmationDueDate', e.target.value)} /></Field></div>
    <p className="text-xs text-muted-foreground">{t('basis.manual')}</p>
  </div>
}

function BasisForm({ base, number, options, onClose, onDone, existing }: { base: string; number: string; options: CoordOptions;
  onClose: () => void; onDone: (id: string) => void; existing?: Detail }) {
  const current = existing?.versions.find(v => v.version.id === existing.entry.currentVersionId)?.version
  const [kind, setKind] = useState(existing?.entry.kind ?? 'Assumption'), [title, setTitle] = useState(existing?.entry.title ?? '')
  const [ownerId, setOwnerId] = useState(existing?.entry.ownerId ?? options.actorId)
  const [disciplineId, setDisciplineId] = useState(existing?.entry.projectDisciplineId ?? '')
  const [approverId, setApproverId] = useState(existing?.entry.independentApproverId ?? '')
  const [draft, setDraft] = useState<VersionDraft>(() => fromVersion(current))
  const [reason, setReason] = useState(''), [busy, setBusy] = useState(false), [error, setError] = useState<unknown>(null)
  const [duplicateId, setDuplicateId] = useState(''), [inspected, setInspected] = useState(false)
  const receipt = useRef<{ signature: string; requestId: string } | null>(null)
  const submit = async (event: React.FormEvent) => {
    event.preventDefault(); setError(null); setBusy(true)
    const body = existing ? { entryRowVersion: existing.entry.rowVersion, currentVersionRowVersion: current!.rowVersion,
      version: payloadVersion(draft), reason } : { kind, title, ownerId, projectDisciplineId: disciplineId,
      independentApproverId: approverId || null, version: payloadVersion(draft), reason: null,
      inspectedDuplicateId: inspected ? duplicateId : null }
    const signature = JSON.stringify(body)
    if (!receipt.current || receipt.current.signature !== signature) receipt.current = { signature, requestId: crypto.randomUUID() }
    try { const result = await post<{ id: string }>(existing ? `${base}/${existing.entry.id}/propose` : base,
      { ...body, requestId: receipt.current.requestId })
      toast.success(t('coord.saved')); onDone(existing?.entry.id ?? result.id)
    } catch (e) { setError(e); if (e instanceof ApiError && e.code === 'basis_duplicate' && typeof e.body.existingId === 'string') {
      setDuplicateId(e.body.existingId); setInspected(false) } } finally { setBusy(false) }
  }
  return <Dialog open onOpenChange={o => !o && !busy && onClose()}><DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
    <DialogHeader><DialogTitle>{t(existing ? 'basis.propose' : 'basis.new')}</DialogTitle><DialogDescription>{t('basis.subtitle')}</DialogDescription></DialogHeader>
    <form onSubmit={submit} className="space-y-4"><fieldset disabled={busy} className="space-y-4">
      {!existing && <><SelectField label={t('basis.kind')} value={kind} onChange={setKind} choices={[{ value: 'Criterion', label: t('basis.criterion') }, { value: 'Assumption', label: t('basis.assumption') }]} />
        <Field label={t('coord.title')} htmlFor="basis-title"><Input id="basis-title" required maxLength={200} value={title} onChange={e => setTitle(e.target.value)} /></Field>
        <SelectField label={t('basis.owner')} value={ownerId} onChange={setOwnerId} choices={options.people.map(p => ({ value: p.id, label: p.displayName }))} />
        <SelectField label={t('basis.discipline')} value={disciplineId} onChange={setDisciplineId} choices={options.disciplines.map(d => ({ value: d.id, label: d.name }))} />
        <SelectField label={t('basis.approver')} value={approverId} onChange={setApproverId} required={false} choices={options.people.map(p => ({ value: p.id, label: p.displayName }))} /></>}
      <VersionFields draft={draft} setDraft={setDraft} />
      {existing && <Field label={t('basis.reason')} htmlFor="basis-reason"><Textarea id="basis-reason" required minLength={5} value={reason} onChange={e => setReason(e.target.value)} /></Field>}
      {duplicateId && !existing && <div className="space-y-2 rounded border border-warn p-3 text-sm"><p>{t('basis.duplicate')}</p>
        <Link className="text-primary underline" to={`/projects/${number}/design-basis?basis=${duplicateId}`} target="_blank" rel="noopener noreferrer">{t('basis.openExisting')}</Link>
        <label className="flex gap-2"><input type="checkbox" checked={inspected} onChange={e => setInspected(e.target.checked)} />{t('basis.inspected')}</label></div>}
    </fieldset>{error != null && <ErrorBanner error={error} />}
      <div className="flex justify-end gap-2"><Button type="button" variant="outline" onClick={onClose}>{t('common.cancel')}</Button><Button disabled={busy || !!duplicateId && !inspected}>{t('common.save')}</Button></div>
    </form></DialogContent></Dialog>
}

function BasisDetail({ base, id, number, options, name, close, refresh }: { base: string; id: string; number: string;
  options?: CoordOptions; name: (id?: string) => string; close: () => void; refresh: () => void }) {
  const q = useQuery({ queryKey: ['design-basis-detail', base, id], queryFn: () => get<Detail>(`${base}/${id}`) })
  const [action, setAction] = useState<'propose' | 'confirm' | 'proceed' | 'use' | null>(null)
  const [selectedImpact, setSelectedImpact] = useState<string | null>(null)
  const row = q.data, current = row?.versions.find(v => v.version.id === row.entry.currentVersionId)?.version
  const proposed = row?.versions.find(v => v.version.status === 'Proposed')?.version
  const done = () => { setAction(null); setSelectedImpact(null); refresh(); q.refetch() }
  if (action === 'propose' && row && options) return <BasisForm base={base} number={number} options={options} existing={row}
    onClose={() => setAction(null)} onDone={done} />
  return <><Dialog open onOpenChange={o => !o && close()}><DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-3xl">
    <DialogHeader><DialogTitle>{row ? `${row.entry.key} · ${row.entry.title}` : t('basis.title')}</DialogTitle>
      <DialogDescription>{t('basis.subtitle')}</DialogDescription></DialogHeader>
    {q.isPending ? <Loading rows={4} /> : q.error ? <ErrorBanner error={q.error} retry={() => q.refetch()} /> : row &&
      <div className="space-y-5 text-sm">
        <p>{row.entry.kind} · {t('basis.owner')}: {name(row.entry.ownerId)} · {t('basis.discipline')}: {options?.disciplines.find(d => d.id === row.entry.projectDisciplineId)?.name ?? t('coord.unavailable')}</p>
        <div className="flex flex-wrap gap-2">{row.canManage && current && !proposed && options && <Button size="sm" variant="outline" onClick={() => setAction('propose')}>{t('basis.propose')}</Button>}
          {row.canConfirm && proposed && <Button size="sm" onClick={() => setAction('confirm')}>{t('basis.confirm')}</Button>}
          {row.canManage && row.entry.kind === 'Assumption' && proposed && <Button size="sm" variant="outline" onClick={() => setAction('proceed')}>{t('basis.proceed')}</Button>}
          {options?.canWrite && [...options.tasks, ...options.deliverables].some(w => w.ownerId === options.actorId) && (current || proposed) &&
            <Button size="sm" variant="outline" onClick={() => setAction('use')}>{t('basis.linkUse')}</Button>}</div>
        <section><h3 className="font-medium">{t('basis.version')}</h3><div className="mt-2 space-y-3">{row.versions.map(({ version: v, sourceMissing }) =>
          <div key={v.id} className="rounded border p-3"><p className="font-medium">{t('basis.version')} {v.number} · {tv(v.status)}{row.entry.currentVersionId === v.id && ` · ${t('basis.current')}`}</p>
            <p>{t('basis.scope')}: {v.scope}</p><p className="whitespace-pre-wrap">{v.statement}{v.numericValue != null && ` · ${v.numericValue} ${v.units ?? ''}`}</p>
            {sourceMissing && <p className="text-warn">{t('basis.missingSource')}</p>}
            <p>{t('basis.manual')}{v.sourceSystem && ` · ${v.sourceSystem}`}{v.stableSourceId && ` · ${v.stableSourceId}`}{v.declaredRevision && ` · ${t('basis.revision')}: ${v.declaredRevision}`}</p>
            {v.sourceUrl && <a className="text-primary underline" href={v.sourceUrl} target="_blank" rel="noopener noreferrer">{t('basis.sourceUrl')}</a>}
            {v.confirmationDueDate && <p>{t('basis.due')}: {fmtDate(v.confirmationDueDate)}</p>}
            {v.confirmedAt && <p>{t('basis.confirm')}: {name(v.confirmedBy)} · {fmtDate(v.confirmedAt)} · {v.confirmationRationale}</p>}
          </div>)}</div></section>
        <section><h3 className="font-medium">{t('basis.conflicts')} ({row.conflicts.filter(c => !c.resolved).length})</h3>
          {!row.conflicts.some(c => !c.resolved) && <p>{t('basis.noConflict')}</p>}
          {row.conflicts.filter(c => !c.resolved).map(c => <div key={c.id} className="rounded border border-warn p-2">
            <p>{c.left.entryKey}: {c.left.numericValue ?? c.left.statement} {c.left.units ?? ''}</p>
            <p>{c.right.entryKey}: {c.right.numericValue ?? c.right.statement} {c.right.units ?? ''}</p>
            <p>{t('basis.scope')}: {c.left.scope}</p></div>)}</section>
        <section><h3 className="font-medium">{t('basis.uses')} ({row.uses.length})</h3><ul className="mt-2 space-y-2">{row.uses.map(u =>
          <li key={u.id} className="rounded border p-2">{options ? <WorkLink options={options} type={u.targetType} id={u.targetId} number={number} /> :
            <Link className="text-primary underline" to={`/projects/${number}/${u.targetType === 'Task' ? 'tasks' : 'deliverables'}?panel=${u.targetType}:${u.targetId}`}>{u.targetType}</Link>}
            {' · '}{t('basis.version')} {row.versions.find(v => v.version.id === u.versionId)?.version.number ?? '?'} · {u.isCurrent ? t('basis.currentUse') : t('basis.historicalUse')}
            {u.isCurrent && row.versions.find(v => v.version.id === u.versionId)?.version.status === 'Superseded' &&
              <strong className="ml-2 text-warn">{t('basis.supersededUse')}</strong>} · {u.intendedUse}</li>)}</ul></section>
        <section><h3 className="font-medium">{t('basis.impacts')} ({row.impacts.length})</h3><ul className="mt-2 space-y-2">{row.impacts.map(i =>
          <li key={i.id} className="rounded border p-2">{tv(i.status)} · {name(i.ownerId)} · {t('basis.version')} {row.versions.find(v => v.version.id === i.oldVersionId)?.version.number} → {row.versions.find(v => v.version.id === i.newVersionId)?.version.number}
            {i.rationale && <p>{t('basis.reason')}: {i.rationale}</p>}{i.evidenceUrl && <a className="text-primary underline" href={i.evidenceUrl} target="_blank" rel="noopener noreferrer">{t('basis.evidence')}</a>}
            {i.status === 'Pending' && options && row.uses.find(u => u.id === i.basisUseId)?.isCurrent &&
              (() => { const u = row.uses.find(u => u.id === i.basisUseId)!; return !!workRef(options, u.targetType, u.targetId) })() &&
              (options.actorId === i.ownerId || row.canManage) &&
              <Button size="sm" variant="outline" onClick={() => setSelectedImpact(i.id)}>{t('basis.decideImpact')}</Button>}</li>)}</ul></section>
        {row.dispositions.length > 0 && <section><h3 className="font-medium">{t('basis.proceed')}</h3><ul>{row.dispositions.map(d =>
          <li key={d.id} className="rounded border p-2">{d.scope} · {name(d.ownerId)} · {t('basis.expiry')}: {fmtDate(d.expiresOn)} · {d.reason}</li>)}</ul></section>}
      </div>}
  </DialogContent></Dialog>
    {row && action === 'confirm' && proposed && <ConfirmForm base={base} id={id} entry={row.entry} version={proposed}
      close={() => setAction(null)} done={done} />}
    {row && action === 'proceed' && proposed && options && <ProceedForm base={base} id={id} version={proposed}
      options={options} close={() => setAction(null)} done={done} />}
    {row && action === 'use' && options && <UseForm base={base} id={id} versions={row.versions.map(v => v.version)}
      currentId={current?.id} options={options} close={() => setAction(null)} done={done} />}
    {row && selectedImpact && options && <ImpactForm base={base} id={id} number={number} row={row} impactId={selectedImpact}
      options={options} close={() => setSelectedImpact(null)} done={done} />}
  </>
}

function ImpactForm({ base, id, number, row, impactId, options, close, done }: { base: string; id: string; number: string; row: Detail;
  impactId: string; options: CoordOptions; close: () => void; done: () => void }) {
  const impact = row.impacts.find(i => i.id === impactId)!, use = row.uses.find(u => u.id === impact.basisUseId)!
  const next = row.versions.find(v => v.version.id === impact.newVersionId)!.version
  const target = workRef(options, use.targetType, use.targetId)
  const canAdopt = options.actorId === use.ownerId, canUnaffected = row.canManage && options.actorId !== use.ownerId
  const [decision, setDecision] = useState(canAdopt ? 'Adopt' : 'Unaffected')
  const [rationale, setRationale] = useState(''), [evidenceUrl, setEvidenceUrl] = useState('')
  return <CommandForm path={`${base}/${id}/impacts/${impactId}/decide`} title={t('basis.decideImpact')}
    hint={t('basis.impactHint')} onClose={close} onDone={done}
    payload={() => ({ assessmentRowVersion: impact.rowVersion, basisUseRowVersion: use.rowVersion,
      newVersionRowVersion: next.rowVersion, targetRowVersion: target?.rowVersion, action: decision, rationale, evidenceUrl })}>
    <p>{t('basis.version')} {row.versions.find(v => v.version.id === impact.oldVersionId)?.version.number} → {next.number}</p>
    {target ? <WorkLink options={options} type={use.targetType} id={use.targetId} number={number} /> : <p>{t('coord.unavailable')}</p>}
    <SelectField label={t('basis.decision')} value={decision} onChange={setDecision}
      choices={[...(canAdopt ? [{ value: 'Adopt', label: t('basis.adopt') }] : []),
        ...(canUnaffected ? [{ value: 'Unaffected', label: t('basis.unaffected') }] : [])]} />
    <Field label={t('basis.reason')} htmlFor="basis-impact-reason"><Textarea id="basis-impact-reason" required minLength={5} value={rationale} onChange={e => setRationale(e.target.value)} /></Field>
    <Field label={t('basis.evidence')} htmlFor="basis-impact-evidence"><Input id="basis-impact-evidence" type="url" required value={evidenceUrl} onChange={e => setEvidenceUrl(e.target.value)} /></Field>
    {!target && <p className="text-warn">{t('basis.targetUnavailable')}</p>}
  </CommandForm>
}

function ProceedForm({ base, id, version, options, close, done }: { base: string; id: string; version: Version;
  options: CoordOptions; close: () => void; done: () => void }) {
  const [ownerId, setOwnerId] = useState(''), [expiry, setExpiry] = useState(''), [reason, setReason] = useState('')
  return <CommandForm path={`${base}/${id}/versions/${version.id}/proceed`} title={t('basis.proceed')}
    hint={t('basis.proceedHint')} onClose={close} onDone={done}
    payload={() => ({ versionRowVersion: version.rowVersion, scope: version.scope, ownerId, expiresOn: expiry, reason })}>
    <p>{t('basis.scope')}: {version.scope}</p><SelectField label={t('basis.owner')} value={ownerId} onChange={setOwnerId}
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
