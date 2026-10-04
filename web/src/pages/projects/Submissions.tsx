import { UrlSearchInput } from '@/components/hub/url-search'
import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Download, Plus } from 'lucide-react'
import { Link, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { ActiveFilters, Empty, ErrorBanner, Field, FilterBar, Loading, Missing, Page, TableRegion, tdCls, thCls, useIsPhone } from '@/components/hub/common'
import { ExportMenu } from '@/components/hub/export'
import { Pill } from '@/components/hub/pills'
import { ViewMenu } from '@/components/hub/views'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { download, get, qs } from '@/lib/api'
import { fmtDate, today } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { useCurrentProject } from './ProjectLayout'
import { CommandForm, CoordStatus, Count, FieldsCommand, PersonLabel, RegisterPager, SelectField, discName, personName, peopleChoices, type CoordOptions, type RecordVersion } from './CoordinationForms'

type Package = RecordVersion & { key: string; title: string; purpose: string; recipientReference: string; coordinatorId: string; milestoneId: string; targetDate: string; status: string; manifestVersion: number; supersedesPackageId?: string }
type Row = Pick<Package, 'id' | 'key' | 'title' | 'status' | 'coordinatorId' | 'targetDate' | 'manifestVersion' | 'rowVersion'> & { blockerCount: number }
type Check = RecordVersion & { kind: string; sourceId?: string; projectDisciplineId?: string; ownerId: string; required: boolean; status: string; evidenceRule?: string; evidenceUrl?: string; reason?: string; approvedBy?: string }
type Blocker = { kind: string; sourceId?: string; disciplineId?: string; ownerId?: string; code: string; message: string; sourcePath?: string }
type Detail = { package: Package; effectiveStatus: string; readiness?: { ready: boolean; fingerprint: string; blockers: Blocker[] }; manifest: { deliverableId: string; sourceRevisionId: string; reviewRoundId?: string; manifestVersion: number }[]; checks: Check[]; evidence: { id: string; checkId: string; evidenceUrl: string; note: string }[]; issue?: { authorisedAt: string; authorisedBy: string; destination: string; transmittalUrl: string; manifestSnapshot: string }; canCoordinate: boolean; canAuthorise: boolean }
type Milestone = { id: string; key: string; name: string; date?: string; isCancelled: boolean }
type Action = { type: 'start' | 'manifest' | 'edit' | 'assign' | 'cancel' | 'issue' | 'check' | 'checkAssign' | 'supersede'; check?: Check; decision?: string }
const fieldset = 'space-y-1 rounded-lg border p-4', legend = 'px-1 text-sm font-semibold', check = 'mt-0.5 size-4 shrink-0 accent-(--primary)'
const subhead = 'text-base/6 font-semibold'

export function SubmissionsTab() {
  const phone = useIsPhone()
  const project = useCurrentProject(), qc = useQueryClient(), [sp, setSp] = useSearchParams(), [adding, setAdding] = useState(false)
  const page = Math.max(1, Number(sp.get('page')) || 1), panel = sp.get('panel')?.startsWith('SubmissionPackage:') ? sp.get('panel')!.slice(18) : null
  const filters = Object.fromEntries(['q', 'status', 'coordinatorId', 'milestoneId', 'targetFrom', 'targetTo'].map(k => [k, sp.get(k) ?? '']))
  const options = useQuery({ queryKey: ['coord-options', project.id], queryFn: () => get<CoordOptions>(`projects/${project.id}/changes/options`) })
  const milestones = useQuery({ queryKey: ['submission-milestones', project.id], queryFn: () => get<Milestone[]>(`projects/${project.id}/milestones?showCompleted=true`) })
  const list = useQuery({ queryKey: ['submissions', project.id, filters, page], queryFn: () => get<{ items: Row[]; pageSize: number; totalCount: number }>(`projects/${project.id}/submissions${qs({ ...filters, page })}`) })
  const refresh = () => { qc.invalidateQueries({ queryKey: ['submissions', project.id] }); qc.invalidateQueries({ queryKey: ['submission-detail', project.id] }); qc.invalidateQueries({ queryKey: ['coord-options', project.id] }) }
  const open = (id?: string) => { const next = new URLSearchParams(sp); if (id) next.set('panel', `SubmissionPackage:${id}`); else next.delete('panel'); setSp(next) }
  const change = (key: string, value: string) => { const next = new URLSearchParams(sp); if (value) next.set(key, value); else next.delete(key); if (key !== 'page' && key !== 'panel') next.delete('page'); setSp(next) }
  const canCreate = project.permissions.isPm || (options.data?.manageDisciplineIds.length ?? 0) > 0
  const milestone = milestones.data?.find(m => m.id === filters.milestoneId)
  // Active filters as removable tokens (§13.0 Filters); the URL keeps the same parameters.
  const tokens = [
    filters.q && { key: 'q', label: t('common.search'), value: filters.q },
    filters.status && { key: 'status', label: t('common.status'), value: tv(filters.status) },
    filters.coordinatorId && { key: 'coordinatorId', label: t('coord.coordinator'), value: options.data ? personName(options.data, filters.coordinatorId) : t('common.dash') },
    filters.milestoneId && { key: 'milestoneId', label: t('submissions.milestone'), value: milestone ? `${milestone.key} · ${milestone.name}` : t('common.dash') },
    filters.targetFrom && { key: 'targetFrom', label: `${t('submissions.targetDate')} ${t('common.from')}`, value: fmtDate(filters.targetFrom) },
    filters.targetTo && { key: 'targetTo', label: `${t('submissions.targetDate')} ${t('common.to')}`, value: fmtDate(filters.targetTo) },
  ].filter(x => !!x)
  const clear = () => { const next = new URLSearchParams(sp); for (const k of Object.keys(filters)) next.delete(k); next.delete('page'); setSp(next) }
  return <Page title={t('submissions.title')} subtitle={t('submissions.subtitle')} actions={<><ViewMenu listType="submissions" projectId={project.id} /><ExportMenu path={`projects/${project.id}/submissions/export`} params={filters} name={`${project.projectNumber}-submissions`} />{canCreate && <Button onClick={() => setAdding(true)}><Plus className="size-4" />{t('submissions.new')}</Button>}</>}>
    {options.error && <ErrorBanner error={options.error} retry={() => options.refetch()} />}
    {milestones.error && <ErrorBanner error={milestones.error} retry={() => milestones.refetch()} />}
    <FilterBar>
      <div className="flex flex-wrap items-end gap-3">
        <Field label={t('common.search')} htmlFor="submissions-search" className="w-full sm:w-56"><UrlSearchInput id="submissions-search" value={filters.q} onValueChange={value => change('q', value)} /></Field>
        <div className="w-full sm:w-40"><SelectField label={t('common.status')} value={filters.status} onChange={v => change('status', v)} required={false} choices={['Draft', 'Checking', 'Ready', 'Issued', 'Superseded', 'Cancelled'].map(s => ({ value: s, label: tv(s) }))} /></div>
        {options.data && <div className="w-full sm:w-48"><SelectField label={t('coord.coordinator')} value={filters.coordinatorId} onChange={v => change('coordinatorId', v)} required={false} choices={peopleChoices(options.data)} /></div>}
        {milestones.data && <div className="w-full sm:w-56"><SelectField label={t('submissions.milestone')} value={filters.milestoneId} onChange={v => change('milestoneId', v)} required={false} choices={milestones.data.map(m => ({ value: m.id, label: `${m.key} · ${m.name}` }))} /></div>}
        <Field label={`${t('submissions.targetDate')} ${t('common.from')}`} htmlFor="submissions-target-from" className="w-full sm:w-44"><Input id="submissions-target-from" type="date" value={filters.targetFrom} onChange={e => change('targetFrom', e.target.value)} /></Field>
        <Field label={`${t('submissions.targetDate')} ${t('common.to')}`} htmlFor="submissions-target-to" className="w-full sm:w-44"><Input id="submissions-target-to" type="date" value={filters.targetTo} onChange={e => change('targetTo', e.target.value)} /></Field>
      </div>
      <ActiveFilters tokens={tokens} onRemove={k => change(k, '')} onClear={clear} />
    </FilterBar>
    {list.isPending ? <div className="rounded-lg border bg-card"><Loading rows={4} /></div> : list.error ? <ErrorBanner error={list.error} retry={() => list.refetch()} /> : <>
      <p role="status" className="text-sm text-muted-foreground">{t('coord.count', { n: list.data.totalCount })}</p>
      {!list.data.items.length ? <div className="rounded-lg border bg-card"><Empty title={t('submissions.empty')}>{t(tokens.length ? 'register.noMatch' : 'submissions.emptyHint')}</Empty></div> :
        phone ? <ul className="space-y-2">{list.data.items.map(row => <li key={row.id} className={cn('space-y-3 rounded-lg border bg-card p-4 text-sm', panel === row.id && 'bg-accent shadow-[inset_3px_0_0_var(--primary)]')}><button className="break-words text-left font-semibold text-primary hover:underline" aria-current={panel === row.id || undefined} onClick={() => open(row.id)}><span className="key font-normal">{row.key}</span> · {row.title}</button><CoordStatus status={row.status} /><dl className="grid grid-cols-[minmax(5rem,auto)_minmax(0,1fr)] gap-x-3 gap-y-2"><dt className="text-muted-foreground">{t('coord.coordinator')}</dt><dd>{options.data ? <PersonLabel options={options.data} id={row.coordinatorId} /> : <Missing />}</dd><dt className="text-muted-foreground">{t('submissions.targetDate')}</dt><dd>{row.targetDate ? fmtDate(row.targetDate) : <Missing />}</dd><dt className="text-muted-foreground">{t('submissions.blockers')}</dt><dd><Count n={row.blockerCount} tone="bad" /></dd></dl></li>)}</ul> :
        <TableRegion><table className="w-full text-left text-sm"><caption className="sr-only">{t('submissions.title')}</caption>
          <thead className="bg-muted"><tr>{['coord.item', 'coord.coordinator', 'common.status', 'submissions.targetDate'].map(k => <th scope="col" key={k} className={thCls}>{t(k)}</th>)}<th scope="col" className={cn(thCls, 'text-right')}>{t('submissions.blockers')}</th></tr></thead>
          <tbody>{list.data.items.map(row => <tr key={row.id} className={cn('border-t hover:bg-muted', panel === row.id && 'bg-accent shadow-[inset_3px_0_0_var(--primary)] hover:bg-accent')}>
            <td className={cn(tdCls, 'min-w-64')}><button className="break-words text-left font-semibold text-primary underline-offset-4 hover:underline" aria-current={panel === row.id || undefined} onClick={() => open(row.id)}><span className="key font-normal">{row.key}</span> · {row.title}</button></td>
            <td className={tdCls}>{options.data ? <PersonLabel options={options.data} id={row.coordinatorId} /> : <Missing />}</td>
            <td className={tdCls}><CoordStatus status={row.status} /></td>
            <td className={cn(tdCls, 'whitespace-nowrap tabular-nums')}>{row.targetDate ? fmtDate(row.targetDate) : <Missing />}</td>
            <td className={cn(tdCls, 'text-right tabular-nums')}><Count n={row.blockerCount} tone="bad" /></td>
          </tr>)}</tbody></table></TableRegion>}
      <RegisterPager label={t('submissions.pager')} page={page} total={list.data.totalCount} pageSize={list.data.pageSize} onPage={n => setSp(p => { const next = new URLSearchParams(p); next.set('page', String(n)); return next })} />
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
      <div className="grid gap-3 sm:grid-cols-2">
        {mode === 'create' && <SelectField label={t('coord.coordinator')} value={coordinator} onChange={setCoordinator} choices={peopleChoices(options)} />}
        <SelectField label={t('submissions.milestone')} value={milestoneId} onChange={setMilestoneId} choices={milestones.filter(m => !m.isCancelled).map(m => ({ value: m.id, label: `${m.key} · ${m.name}` }))} />
        <Field label={t('submissions.targetDate')} htmlFor="sub-date"><Input id="sub-date" required type="date" value={targetDate} onChange={e => setTargetDate(e.target.value)} /></Field>
      </div></>}
    {mode !== 'edit' && <fieldset className={fieldset}><legend className={legend}>{t('submissions.manifest')}</legend><p className="pb-2 text-xs/[18px] text-muted-foreground">{t('submissions.manifestHint')}</p>{available.map(s => <label key={s.revision.id} className="flex items-start gap-2 py-1.5 text-sm"><input type="checkbox" className={check} checked={sources.includes(s.revision.id)} onChange={e => setSources(v => e.target.checked ? [...v, s.revision.id] : v.filter(x => x !== s.revision.id))} />{s.revision.sourceKey} · {s.revision.revision} · {s.revision.title}</label>)}{!available.length && <p className="text-sm text-muted-foreground">{t('review.noSources')}</p>}</fieldset>}
    {mode === 'create' && <fieldset className={cn(fieldset, 'space-y-3')}><legend className={legend}>{t('submissions.optional')}</legend>{optionals.map((item, index) => <div key={item.id} className="space-y-3 rounded-md bg-muted p-3"><Field label={`${t('submissions.optionalLabel')} ${index + 1}`} htmlFor={`sub-opt-${item.id}`}><Input id={`sub-opt-${item.id}`} required maxLength={500} value={item.label} onChange={e => setOptionals(rows => rows.map(o => o.id === item.id ? { ...o, label: e.target.value } : o))} /></Field><SelectField label={t('coord.owner')} value={item.ownerId} onChange={ownerId => setOptionals(rows => rows.map(o => o.id === item.id ? { ...o, ownerId } : o))} choices={peopleChoices(options)} /><Button type="button" size="sm" variant="outline" onClick={() => setOptionals(rows => rows.filter(o => o.id !== item.id))}>{t('submissions.removeOptional')}</Button></div>)}<Button type="button" size="sm" variant="outline" disabled={optionals.length >= 50} onClick={() => setOptionals(rows => [...rows, { id: crypto.randomUUID(), label: '', ownerId: options.actorId }])}><Plus className="size-4" />{t('submissions.addOptional')}</Button></fieldset>}
    {(mode !== 'create' || supersedes) && <Field label={t('coord.reason')} htmlFor="sub-reason"><Textarea id="sub-reason" required minLength={5} value={reason} onChange={e => setReason(e.target.value)} /></Field>}
  </CommandForm>
}

/** A check's result as the §13.0 status language; a derived check that currently passes says it is a live check. */
function CheckStatus({ check, detail }: { check: Check; detail: Detail }) {
  // Derived checks are recorded as Pass only at Issue, after the source fingerprint is rechecked.
  if (check.kind !== 'Applicability' && check.status === 'Pending' && detail.readiness?.ready &&
      ['Checking', 'Ready'].includes(detail.package.status)) return <Pill tone="done">{t('submissions.livePass')}</Pill>
  return <CoordStatus status={check.status} />
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
    {q.isPending ? <Loading rows={4} /> : q.error ? <ErrorBanner error={q.error} retry={() => q.refetch()} /> : d && p && <div className="space-y-6 text-sm">
      <div className="flex flex-wrap items-center gap-2"><CoordStatus status={d.effectiveStatus} /><span className="rounded-md bg-secondary px-2 py-0.5 text-xs/[18px] font-medium tabular-nums">{t('submissions.version', { n: p.manifestVersion })}</span><span className="flex-1" /><Button size="sm" variant="outline" onClick={exportFile}><Download className="size-4" />{t('submissions.export')}</Button>
        {d.canCoordinate && p.status === 'Draft' && <Button size="sm" onClick={() => setAction({ type: 'start' })}>{t('submissions.start')}</Button>}
        {d.canCoordinate && ['Draft', 'Checking', 'Ready'].includes(p.status) && <><Button size="sm" variant="outline" onClick={() => setAction({ type: 'manifest' })}>{t('submissions.replaceManifest')}</Button><Button size="sm" variant="outline" onClick={() => setAction({ type: 'edit' })}>{t('submissions.edit')}</Button></>}
        {d.canAuthorise && ['Draft', 'Checking', 'Ready'].includes(p.status) && <><Button size="sm" variant="outline" onClick={() => setAction({ type: 'assign' })}>{t('submissions.assign')}</Button><Button size="sm" variant="ghost" className="text-bad hover:text-bad" onClick={() => setAction({ type: 'cancel' })}>{t('review.cancel')}</Button></>}
        {d.canAuthorise && d.readiness?.ready && ['Checking', 'Ready'].includes(p.status) && <Button size="sm" onClick={() => setAction({ type: 'issue' })}>{t('submissions.issue')}</Button>}
        {p.status === 'Issued' && (d.canAuthorise || options.manageDisciplineIds.length > 0) && <Button size="sm" variant="outline" onClick={() => setAction({ type: 'supersede' })}>{t('submissions.supersede')}</Button>}
      </div>
      <dl className="grid grid-cols-[minmax(8rem,auto)_1fr] gap-x-4 gap-y-2">
        <dt className="text-muted-foreground">{t('coord.coordinator')}</dt><dd><PersonLabel options={options} id={p.coordinatorId} /></dd>
        <dt className="text-muted-foreground">{t('submissions.milestone')}</dt><dd>{milestones.find(m => m.id === p.milestoneId)?.name ?? <span className="text-muted-foreground">{t('coord.unavailable')}</span>}</dd>
        <dt className="text-muted-foreground">{t('submissions.targetDate')}</dt><dd className="tabular-nums">{fmtDate(p.targetDate)}</dd>
        <dt className="text-muted-foreground">{t('submissions.recipient')}</dt><dd className="break-words">{p.recipientReference}</dd>
      </dl>
      <p className="whitespace-pre-wrap text-base/6">{p.purpose}</p>
      <section className="space-y-2"><h3 className={subhead}>{t('submissions.manifest')}</h3><ul className="space-y-2">{d.manifest.map(m => { const s = options.sources.find(s => s.revision.id === m.sourceRevisionId)?.revision; return <li key={m.sourceRevisionId} className="rounded-md border px-3 py-2">{s ? <a className="break-words text-primary underline underline-offset-4" href={s.url} target="_blank" rel="noopener noreferrer"><span className="key">{s.sourceKey}</span> · {s.revision} · {s.title} · {t('change.registered')}</a> : <Link className="break-all text-primary underline underline-offset-4" to={`/projects/${number}/changes`}>{m.sourceRevisionId}</Link>}</li> })}</ul></section>
      {d.readiness && <section aria-live="polite" className="space-y-3"><h3 className={subhead}>{t('submissions.blockers')} <span className="ml-1 rounded-md bg-secondary px-2 py-0.5 text-xs font-medium text-muted-foreground tabular-nums">{d.readiness.blockers.length}</span></h3>{d.readiness.ready ? <p className="flex gap-2 rounded-md border border-done/30 bg-done-bg px-4 py-3 text-done"><span aria-hidden>✓</span><span>{t('submissions.ready')}</span></p> : Object.entries(grouped).map(([key, blockers]) => { const [discipline, owner] = key.split(':'); return <div key={key} className="space-y-2 rounded-lg border border-bad/30 p-4"><h4 className="flex flex-wrap items-center gap-x-2 gap-y-1 font-semibold"><span>{discipline ? discName(options, discipline) : t('submissions.general')}</span><span aria-hidden className="text-muted-foreground">·</span>{owner ? <PersonLabel options={options} id={owner} /> : <span className="font-normal text-muted-foreground">{t('ind.unassigned')}</span>}</h4><ul className="space-y-1">{blockers?.map((b, i) => <li key={`${b.code}-${b.sourceId ?? i}`} className="flex gap-2"><span aria-hidden className="text-bad">■</span><span>{b.sourcePath ? <Link className="text-primary underline underline-offset-4" to={b.sourcePath}>{b.message}</Link> : b.message}</span></li>)}</ul></div> })}</section>}
      <section className="space-y-2"><h3 className={subhead}>{t('submissions.checks')}</h3><div className="space-y-2">{d.checks.map(c => <div key={c.id} className="space-y-2 rounded-lg border p-4"><div className="flex flex-wrap items-center gap-2"><strong className="font-semibold">{c.kind === 'Applicability' ? c.evidenceRule : c.kind}</strong><span className="text-muted-foreground">·</span><PersonLabel options={options} id={c.ownerId} /><CheckStatus check={c} detail={d} />{c.kind === 'Applicability' && ['Checking', 'Ready'].includes(p.status) && <span className="flex flex-wrap gap-2 sm:ml-auto">{options.actorId === c.ownerId && <><Button size="sm" variant="outline" onClick={() => setAction({ type: 'check', check: c, decision: 'Pass' })}>{t('submissions.pass')}</Button><Button size="sm" variant="outline" onClick={() => setAction({ type: 'check', check: c, decision: 'Fail' })}>{tv('Fail')}</Button></>}{d.canAuthorise && <><Button size="sm" variant="outline" onClick={() => setAction({ type: 'check', check: c, decision: 'Not Applicable' })}>{t('submissions.notApplicable')}</Button><Button size="sm" variant="outline" onClick={() => setAction({ type: 'checkAssign', check: c })}>{t('submissions.assign')}</Button></>}</span>}</div>{c.evidenceUrl && <a href={c.evidenceUrl} target="_blank" rel="noopener noreferrer" className="text-primary underline underline-offset-4">{t('coord.evidence')}</a>}{c.reason && <p className="whitespace-pre-wrap">{c.reason}</p>}</div>)}</div></section>
      {d.issue && <section className="space-y-1 rounded-lg border border-done/30 bg-done-bg p-4"><h3 className={subhead}>{t('submissions.issued')}</h3><p><span className="tabular-nums">{fmtDate(d.issue.authorisedAt)}</span> · {personName(options, d.issue.authorisedBy)} · {d.issue.destination}</p><a className="text-primary underline underline-offset-4" href={d.issue.transmittalUrl} target="_blank" rel="noopener noreferrer">{t('submissions.transmittal')}</a></section>}
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
  if (action.type === 'check' && c) return <FieldsCommand path={`${base}/checks/${c.id}`} title={action.decision === 'Not Applicable' ? t('submissions.notApplicable') : action.decision === 'Fail' ? tv('Fail') : t('submissions.pass')} fields={[{ name: 'evidenceUrl', label: 'coord.evidenceUrl', type: 'url' }, { name: 'reason', label: 'coord.reason', type: 'textarea', optional: action.decision === 'Pass' }]} build={v => ({ ...v, action: action.decision, packageRowVersion: p.rowVersion, rowVersion: c.rowVersion })} onClose={close} onDone={done} />
  return null
}
