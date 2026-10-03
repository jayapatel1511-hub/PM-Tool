import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { ErrorBanner, Field, Loading, Page } from '@/components/hub/common'
import { ExportMenu } from '@/components/hub/export'
import { ViewMenu } from '@/components/hub/views'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { download, get, qs } from '@/lib/api'
import { fmtDate, today } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { useCurrentProject } from './ProjectLayout'
import { CommandForm, FieldsCommand, SelectField, discName, personName, peopleChoices, type CoordOptions, type RecordVersion } from './CoordinationForms'

type Package = RecordVersion & { key: string; title: string; purpose: string; recipientReference: string; coordinatorId: string; milestoneId: string; targetDate: string; status: string; manifestVersion: number; supersedesPackageId?: string }
type Row = Pick<Package, 'id' | 'key' | 'title' | 'status' | 'coordinatorId' | 'targetDate' | 'manifestVersion' | 'rowVersion'> & { blockerCount: number }
type Check = RecordVersion & { kind: string; sourceId?: string; projectDisciplineId?: string; ownerId: string; required: boolean; status: string; evidenceRule?: string; evidenceUrl?: string; reason?: string; approvedBy?: string }
type Blocker = { kind: string; sourceId?: string; disciplineId?: string; ownerId?: string; code: string; message: string; sourcePath?: string }
type Detail = { package: Package; effectiveStatus: string; readiness?: { ready: boolean; fingerprint: string; blockers: Blocker[] }; manifest: { deliverableId: string; sourceRevisionId: string; reviewRoundId?: string; manifestVersion: number }[]; checks: Check[]; evidence: { id: string; checkId: string; evidenceUrl: string; note: string }[]; issue?: { authorisedAt: string; authorisedBy: string; destination: string; transmittalUrl: string; manifestSnapshot: string }; canCoordinate: boolean; canAuthorise: boolean }
type Milestone = { id: string; key: string; name: string; date?: string; isCancelled: boolean }
type Action = { type: 'start' | 'manifest' | 'edit' | 'assign' | 'cancel' | 'issue' | 'check' | 'checkAssign' | 'supersede'; check?: Check; decision?: string }

export function SubmissionsTab() {
  const project = useCurrentProject(), qc = useQueryClient(), [sp, setSp] = useSearchParams(), [adding, setAdding] = useState(false)
  const page = Math.max(1, Number(sp.get('page')) || 1), panel = sp.get('panel')?.startsWith('SubmissionPackage:') ? sp.get('panel')!.slice(18) : null
  const filters = Object.fromEntries(['q', 'status', 'coordinatorId', 'milestoneId', 'targetFrom', 'targetTo'].map(k => [k, sp.get(k) ?? '']))
  const options = useQuery({ queryKey: ['coord-options', project.id], queryFn: () => get<CoordOptions>(`projects/${project.id}/changes/options`) })
  const milestones = useQuery({ queryKey: ['submission-milestones', project.id], queryFn: () => get<Milestone[]>(`projects/${project.id}/milestones?showCompleted=true`) })
  const list = useQuery({ queryKey: ['submissions', project.id, filters, page], queryFn: () => get<{ items: Row[]; pageSize: number; totalCount: number }>(`projects/${project.id}/submissions${qs({ ...filters, page })}`) })
  const refresh = () => { qc.invalidateQueries({ queryKey: ['submissions', project.id] }); qc.invalidateQueries({ queryKey: ['submission-detail', project.id] }); qc.invalidateQueries({ queryKey: ['coord-options', project.id] }) }
  const open = (id?: string) => { const next = new URLSearchParams(sp); if (id) next.set('panel', `SubmissionPackage:${id}`); else next.delete('panel'); setSp(next) }
  const change = (key: string, value: string) => { const next = new URLSearchParams(sp); value ? next.set(key, value) : next.delete(key); if (key !== 'page' && key !== 'panel') next.delete('page'); setSp(next) }
  const canCreate = project.permissions.isPm || (options.data?.manageDisciplineIds.length ?? 0) > 0
  return <Page title={t('submissions.title')} subtitle={t('submissions.subtitle')} actions={<><ViewMenu listType="submissions" projectId={project.id} /><ExportMenu path={`projects/${project.id}/submissions/export`} params={filters} name={`${project.projectNumber}-submissions`} />{canCreate && <Button size="sm" onClick={() => setAdding(true)}>{t('submissions.new')}</Button>}</>}>
    {options.error && <ErrorBanner error={options.error} retry={() => options.refetch()} />}
    {milestones.error && <ErrorBanner error={milestones.error} retry={() => milestones.refetch()} />}
    <div className="flex flex-wrap items-end gap-3 rounded-lg border bg-card p-3"><Field label={t('common.search')} htmlFor="submissions-search"><Input id="submissions-search" type="search" value={filters.q} onChange={e => change('q', e.target.value)} /></Field>
      <SelectField label={t('common.status')} value={filters.status} onChange={v => change('status', v)} required={false} choices={['Draft', 'Checking', 'Ready', 'Issued', 'Superseded', 'Cancelled'].map(s => ({ value: s, label: tv(s) }))} />
      {options.data && <SelectField label={t('coord.coordinator')} value={filters.coordinatorId} onChange={v => change('coordinatorId', v)} required={false} choices={peopleChoices(options.data)} />}
      {milestones.data && <SelectField label={t('submissions.milestone')} value={filters.milestoneId} onChange={v => change('milestoneId', v)} required={false} choices={milestones.data.map(m => ({ value: m.id, label: `${m.key} · ${m.name}` }))} />}
      <Field label={`${t('submissions.targetDate')} ${t('common.from')}`} htmlFor="submissions-target-from"><Input id="submissions-target-from" type="date" value={filters.targetFrom} onChange={e => change('targetFrom', e.target.value)} /></Field>
      <Field label={`${t('submissions.targetDate')} ${t('common.to')}`} htmlFor="submissions-target-to"><Input id="submissions-target-to" type="date" value={filters.targetTo} onChange={e => change('targetTo', e.target.value)} /></Field>
    </div>
    {list.isPending ? <Loading rows={4} /> : list.error ? <ErrorBanner error={list.error} retry={() => list.refetch()} /> : <>
      <p role="status" className="text-sm text-muted-foreground">{t('coord.count', { n: list.data.totalCount })}</p>
      {!list.data.items.length ? <div className="rounded-lg border bg-card p-8 text-center"><h2 className="font-medium">{t('submissions.empty')}</h2><p className="mt-2 text-sm text-muted-foreground">{t('submissions.emptyHint')}</p></div> :
        <div className="overflow-x-auto rounded-lg border bg-card"><table className="w-full text-left text-sm"><caption className="sr-only">{t('submissions.title')}</caption><thead className="bg-muted/60"><tr>{['coord.item', 'coord.coordinator', 'common.status', 'submissions.targetDate', 'submissions.blockers'].map(k => <th scope="col" key={k} className="p-3 font-medium">{t(k)}</th>)}</tr></thead><tbody>{list.data.items.map(row => <tr key={row.id} className="border-t"><td className="p-3"><button className="text-left font-medium text-primary hover:underline" onClick={() => open(row.id)}>{row.key} · {row.title}</button></td><td className="p-3">{options.data && personName(options.data, row.coordinatorId)}</td><td className="p-3">{tv(row.status)}</td><td className="p-3 whitespace-nowrap">{fmtDate(row.targetDate)}</td><td className="p-3">{row.blockerCount}</td></tr>)}</tbody></table></div>}
      <div className="flex items-center justify-end gap-3"><Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setSp(p => { const n = new URLSearchParams(p); n.set('page', String(page - 1)); return n })}>{t('handoff.previous')}</Button><span className="text-sm">{t('handoff.page', { n: page })}</span><Button size="sm" variant="outline" disabled={page * list.data.pageSize >= list.data.totalCount} onClick={() => setSp(p => { const n = new URLSearchParams(p); n.set('page', String(page + 1)); return n })}>{t('handoff.next')}</Button></div>
    </>}
    {adding && options.data && milestones.data && <SubmissionForm projectId={project.id} options={options.data} milestones={milestones.data} onClose={() => setAdding(false)} onDone={id => { setAdding(false); refresh(); open(id) }} />}
    {panel && options.data && milestones.data && <SubmissionDetail projectId={project.id} number={project.projectNumber} id={panel} options={options.data} milestones={milestones.data} close={() => open()} onCreated={id => { refresh(); open(id) }} refresh={refresh} />}
  </Page>
}

function SubmissionForm({ projectId, options, milestones, onClose, onDone, existing, supersedes, mode = 'create' }: { projectId: string; options: CoordOptions; milestones: Milestone[]; onClose: () => void; onDone: (id: string) => void; existing?: Detail; supersedes?: Detail; mode?: 'create' | 'manifest' | 'edit' }) {
  const p = existing?.package, initial = p ?? supersedes?.package
  const [title, setTitle] = useState(initial?.title ?? ''), [purpose, setPurpose] = useState(initial?.purpose ?? '')
  const [recipient, setRecipient] = useState(initial?.recipientReference ?? ''), [coordinator, setCoordinator] = useState(initial?.coordinatorId ?? options.actorId)
  const [milestoneId, setMilestoneId] = useState(initial?.milestoneId ?? ''), [targetDate, setTargetDate] = useState(initial?.targetDate ?? today())
  const [sources, setSources] = useState<string[]>((existing ?? supersedes)?.manifest.map(m => m.sourceRevisionId) ?? [])
  const [optionals, setOptionals] = useState<{ id: string; label: string; ownerId: string }[]>(() => supersedes?.checks.filter(c => c.kind === 'Applicability').map(c => ({ id: crypto.randomUUID(), label: c.evidenceRule ?? '', ownerId: c.ownerId })) ?? [])
  const [reason, setReason] = useState('')
  const available = options.sources.filter(s => s.published && s.isCurrent && s.revision.deliverableId)
  const path = `projects/${projectId}/submissions${p ? `/${p.id}/${mode === 'manifest' ? 'manifest' : 'edit'}` : ''}`
  const payload = () => mode === 'manifest' ? { rowVersion: p!.rowVersion, manifest: sources.map(sourceRevisionId => ({ sourceRevisionId })), reason } : mode === 'edit' ?
    { rowVersion: p!.rowVersion, title, purpose, recipientReference: recipient, milestoneId, targetDate, reason } :
    { title, purpose, recipientReference: recipient, coordinatorId: coordinator, milestoneId, targetDate, manifest: sources.map(sourceRevisionId => ({ sourceRevisionId })), optionalChecks: optionals.filter(o => o.label.trim()).map(o => ({ label: o.label.trim(), ownerId: o.ownerId, projectDisciplineId: null })), supersedesPackageId: supersedes?.package.id ?? null, reason: supersedes ? reason : null }
  return <CommandForm path={path} title={t(mode === 'manifest' ? 'submissions.replaceManifest' : mode === 'edit' ? 'submissions.edit' : 'submissions.new')} hint={t('submissions.formHint')} payload={payload} onClose={onClose} onDone={onDone}>
    {mode !== 'manifest' && <><Field label={t('coord.title')} htmlFor="sub-title"><Input id="sub-title" required maxLength={200} value={title} onChange={e => setTitle(e.target.value)} /></Field>
      <Field label={t('submissions.purpose')} htmlFor="sub-purpose"><Textarea id="sub-purpose" required maxLength={2000} value={purpose} onChange={e => setPurpose(e.target.value)} /></Field>
      <Field label={t('submissions.recipient')} htmlFor="sub-recipient"><Input id="sub-recipient" required maxLength={1000} value={recipient} onChange={e => setRecipient(e.target.value)} /></Field>
      {mode === 'create' && <SelectField label={t('coord.coordinator')} value={coordinator} onChange={setCoordinator} choices={peopleChoices(options)} />}
      <SelectField label={t('submissions.milestone')} value={milestoneId} onChange={setMilestoneId} choices={milestones.filter(m => !m.isCancelled).map(m => ({ value: m.id, label: `${m.key} · ${m.name}` }))} />
      <Field label={t('submissions.targetDate')} htmlFor="sub-date"><Input id="sub-date" required type="date" value={targetDate} onChange={e => setTargetDate(e.target.value)} /></Field></>}
    {mode !== 'edit' && <fieldset className="rounded border p-3"><legend className="px-1 text-sm font-medium">{t('submissions.manifest')}</legend><p className="text-xs text-muted-foreground">{t('submissions.manifestHint')}</p>{available.map(s => <label key={s.revision.id} className="flex items-start gap-2 py-2 text-sm"><input type="checkbox" checked={sources.includes(s.revision.id)} onChange={e => setSources(v => e.target.checked ? [...v, s.revision.id] : v.filter(x => x !== s.revision.id))} />{s.revision.sourceKey} · {s.revision.revision} · {s.revision.title}</label>)}{!available.length && <p className="text-sm">{t('review.noSources')}</p>}</fieldset>}
    {mode === 'create' && <fieldset className="rounded border p-3"><legend className="px-1 text-sm font-medium">{t('submissions.optional')}</legend>{optionals.map((item, index) => <div key={item.id} className="mb-3 rounded border p-3"><Field label={`${t('submissions.optionalLabel')} ${index + 1}`} htmlFor={`sub-opt-${item.id}`}><Input id={`sub-opt-${item.id}`} required maxLength={500} value={item.label} onChange={e => setOptionals(rows => rows.map(o => o.id === item.id ? { ...o, label: e.target.value } : o))} /></Field><SelectField label={t('coord.owner')} value={item.ownerId} onChange={ownerId => setOptionals(rows => rows.map(o => o.id === item.id ? { ...o, ownerId } : o))} choices={peopleChoices(options)} /><Button type="button" size="sm" variant="outline" onClick={() => setOptionals(rows => rows.filter(o => o.id !== item.id))}>{t('submissions.removeOptional')}</Button></div>)}<Button type="button" size="sm" variant="outline" disabled={optionals.length >= 50} onClick={() => setOptionals(rows => [...rows, { id: crypto.randomUUID(), label: '', ownerId: options.actorId }])}>{t('submissions.addOptional')}</Button></fieldset>}
    {(mode !== 'create' || supersedes) && <Field label={t('coord.reason')} htmlFor="sub-reason"><Textarea id="sub-reason" required minLength={5} value={reason} onChange={e => setReason(e.target.value)} /></Field>}
  </CommandForm>
}

function checkStatus(check: Check, detail: Detail) {
  // Derived checks are recorded as Pass only at Issue, after the source fingerprint is rechecked.
  if (check.kind !== 'Applicability' && check.status === 'Pending' && detail.readiness?.ready &&
      ['Checking', 'Ready'].includes(detail.package.status)) return t('submissions.livePass')
  return tv(check.status)
}

function SubmissionDetail({ projectId, number, id, options, milestones, close, onCreated, refresh }: { projectId: string; number: string; id: string; options: CoordOptions; milestones: Milestone[]; close: () => void; onCreated: (id: string) => void; refresh: () => void }) {
  const q = useQuery({ queryKey: ['submission-detail', projectId, id], queryFn: () => get<Detail>(`projects/${projectId}/submissions/${id}`) })
  const [action, setAction] = useState<Action | null>(null), d = q.data, p = d?.package
  const done = () => { setAction(null); refresh(); q.refetch() }
  if (action?.type === 'manifest' && d) return <SubmissionForm mode="manifest" existing={d} projectId={projectId} options={options} milestones={milestones} onClose={() => setAction(null)} onDone={done} />
  if (action?.type === 'edit' && d) return <SubmissionForm mode="edit" existing={d} projectId={projectId} options={options} milestones={milestones} onClose={() => setAction(null)} onDone={done} />
  if (action?.type === 'supersede' && d) return <SubmissionForm supersedes={d} projectId={projectId} options={options} milestones={milestones} onClose={() => setAction(null)} onDone={onCreated} />
  const grouped = (d?.readiness?.blockers ?? []).reduce<Record<string, Blocker[]>>((groups, blocker) => {
    const key = `${blocker.disciplineId ?? ''}:${blocker.ownerId ?? ''}`
    ;(groups[key] ??= []).push(blocker)
    return groups
  }, {})
  const exportFile = () => download(`/api/v1/projects/${projectId}/submissions/${id}/export`, `${p?.key ?? 'submission'}.json`).catch(e => toast.error((e as Error).message))
  return <><Dialog open onOpenChange={v => !v && close()}><DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-4xl"><DialogHeader><DialogTitle>{p ? `${p.key} · ${p.title}` : t('submissions.title')}</DialogTitle><DialogDescription>{t('submissions.detailHint')}</DialogDescription></DialogHeader>
    {q.isPending ? <Loading rows={4} /> : q.error ? <ErrorBanner error={q.error} retry={() => q.refetch()} /> : d && p && <div className="space-y-5 text-sm">
      <div className="flex flex-wrap items-center gap-2"><strong className="rounded border px-2 py-1">{tv(d.effectiveStatus)}</strong><span>{t('submissions.version', { n: p.manifestVersion })}</span><Button size="sm" variant="outline" onClick={exportFile}>{t('submissions.export')}</Button>
        {d.canCoordinate && p.status === 'Draft' && <Button size="sm" onClick={() => setAction({ type: 'start' })}>{t('submissions.start')}</Button>}
        {d.canCoordinate && ['Draft', 'Checking', 'Ready'].includes(p.status) && <><Button size="sm" variant="outline" onClick={() => setAction({ type: 'manifest' })}>{t('submissions.replaceManifest')}</Button><Button size="sm" variant="outline" onClick={() => setAction({ type: 'edit' })}>{t('submissions.edit')}</Button></>}
        {d.canAuthorise && ['Draft', 'Checking', 'Ready'].includes(p.status) && <><Button size="sm" variant="outline" onClick={() => setAction({ type: 'assign' })}>{t('submissions.assign')}</Button><Button size="sm" variant="outline" onClick={() => setAction({ type: 'cancel' })}>{t('review.cancel')}</Button></>}
        {d.canAuthorise && d.readiness?.ready && ['Checking', 'Ready'].includes(p.status) && <Button size="sm" onClick={() => setAction({ type: 'issue' })}>{t('submissions.issue')}</Button>}
        {p.status === 'Issued' && (d.canAuthorise || options.manageDisciplineIds.length > 0) && <Button size="sm" variant="outline" onClick={() => setAction({ type: 'supersede' })}>{t('submissions.supersede')}</Button>}
      </div>
      <p>{t('coord.coordinator')}: {personName(options, p.coordinatorId)} · {t('submissions.milestone')}: {milestones.find(m => m.id === p.milestoneId)?.name ?? t('coord.unavailable')} · {t('submissions.targetDate')}: {fmtDate(p.targetDate)}</p>
      <p className="whitespace-pre-wrap">{p.purpose}</p><p>{t('submissions.recipient')}: {p.recipientReference}</p>
      <section><h3 className="font-medium">{t('submissions.manifest')}</h3><ul className="mt-2 space-y-2">{d.manifest.map(m => { const s = options.sources.find(s => s.revision.id === m.sourceRevisionId)?.revision; return <li key={m.sourceRevisionId} className="rounded border p-2">{s ? <a className="text-primary underline" href={s.url} target="_blank" rel="noopener noreferrer">{s.sourceKey} · {s.revision} · {s.title}</a> : <Link className="text-primary underline" to={`/projects/${number}/changes`}>{m.sourceRevisionId}</Link>}</li> })}</ul></section>
      {d.readiness && <section aria-live="polite"><h3 className="font-medium">{t('submissions.blockers')} ({d.readiness.blockers.length})</h3>{d.readiness.ready ? <p className="mt-2 rounded border border-done/30 bg-done-bg p-3">{t('submissions.ready')}</p> : Object.entries(grouped).map(([key, blockers]) => { const [discipline, owner] = key.split(':'); return <div key={key} className="mt-3 rounded border p-3"><h4 className="font-medium">{discipline ? discName(options, discipline) : t('submissions.general')} · {owner ? personName(options, owner) : t('coord.unavailable')}</h4><ul className="mt-2 list-inside list-disc space-y-1">{blockers?.map((b, i) => <li key={`${b.code}-${b.sourceId ?? i}`}>{b.sourcePath ? <Link className="text-primary underline" to={b.sourcePath}>{b.message}</Link> : b.message}</li>)}</ul></div> })}</section>}
      <section><h3 className="font-medium">{t('submissions.checks')}</h3><div className="mt-2 space-y-2">{d.checks.map(c => <div key={c.id} className="rounded border p-3"><div className="flex flex-wrap items-center gap-2"><strong>{c.kind === 'Applicability' ? c.evidenceRule : c.kind}</strong><span>· {personName(options, c.ownerId)}</span><span>· {checkStatus(c, d)}</span>{c.kind === 'Applicability' && ['Checking', 'Ready'].includes(p.status) && <>{options.actorId === c.ownerId && <Button size="sm" variant="outline" onClick={() => setAction({ type: 'check', check: c, decision: 'Pass' })}>{t('submissions.pass')}</Button>}{d.canAuthorise && <><Button size="sm" variant="outline" onClick={() => setAction({ type: 'check', check: c, decision: 'Not Applicable' })}>{t('submissions.notApplicable')}</Button><Button size="sm" variant="outline" onClick={() => setAction({ type: 'checkAssign', check: c })}>{t('submissions.assign')}</Button></>}</>}</div>{c.evidenceUrl && <a href={c.evidenceUrl} target="_blank" rel="noopener noreferrer" className="text-primary underline">{t('coord.evidence')}</a>}{c.reason && <p className="whitespace-pre-wrap">{c.reason}</p>}</div>)}</div></section>
      {d.issue && <section className="rounded border p-3"><h3 className="font-medium">{t('submissions.issued')}</h3><p>{fmtDate(d.issue.authorisedAt)} · {personName(options, d.issue.authorisedBy)} · {d.issue.destination}</p><a className="text-primary underline" href={d.issue.transmittalUrl} target="_blank" rel="noopener noreferrer">{t('submissions.transmittal')}</a></section>}
    </div>}
  </DialogContent></Dialog>{action && d && <SubmissionAction projectId={projectId} detail={d} action={action} options={options} close={() => setAction(null)} done={done} />}</>
}

function SubmissionAction({ projectId, detail: d, action, options, close, done }: { projectId: string; detail: Detail; action: Action; options: CoordOptions; close: () => void; done: () => void }) {
  const p = d.package, c = action.check, base = `projects/${projectId}/submissions/${p.id}`
  if (action.type === 'start') return <CommandForm path={`${base}/start`} title={t('submissions.start')} payload={() => ({ rowVersion: p.rowVersion })} onClose={close} onDone={done} />
  if (action.type === 'issue') return <FieldsCommand path={`${base}/issue`} title={t('submissions.issue')} hint={t('submissions.issueHint')} fields={[{ name: 'destination', label: 'submissions.destination' }, { name: 'transmittalUrl', label: 'submissions.transmittal', type: 'url' }]} initial={{ destination: p.recipientReference }} build={v => ({ ...v, rowVersion: p.rowVersion, manifestVersion: p.manifestVersion, expectedFingerprint: d.readiness?.fingerprint })} onClose={close} onDone={done} />
  if (action.type === 'assign') return <FieldsCommand path={`${base}/assign`} title={t('submissions.assign')} fields={[{ name: 'coordinatorId', label: 'coord.coordinator', choices: peopleChoices(options) }, { name: 'reason', label: 'coord.reason', type: 'textarea' }]} build={v => ({ ...v, rowVersion: p.rowVersion })} onClose={close} onDone={done} />
  if (action.type === 'cancel') return <FieldsCommand path={`${base}/cancel`} title={t('review.cancel')} fields={[{ name: 'reason', label: 'coord.reason', type: 'textarea' }]} build={v => ({ ...v, rowVersion: p.rowVersion })} onClose={close} onDone={done} />
  if (action.type === 'checkAssign' && c) return <FieldsCommand path={`${base}/checks/${c.id}/assign`} title={t('submissions.assign')} fields={[{ name: 'ownerId', label: 'coord.owner', choices: peopleChoices(options) }, { name: 'reason', label: 'coord.reason', type: 'textarea' }]} build={v => ({ ...v, packageRowVersion: p.rowVersion, rowVersion: c.rowVersion })} onClose={close} onDone={done} />
  if (action.type === 'check' && c) return <FieldsCommand path={`${base}/checks/${c.id}`} title={t(action.decision === 'Not Applicable' ? 'submissions.notApplicable' : 'submissions.pass')} fields={[{ name: 'evidenceUrl', label: 'coord.evidenceUrl', type: 'url' }, { name: 'reason', label: 'coord.reason', type: 'textarea', optional: action.decision === 'Pass' }]} build={v => ({ ...v, action: action.decision, packageRowVersion: p.rowVersion, rowVersion: c.rowVersion })} onClose={close} onDone={done} />
  return null
}
