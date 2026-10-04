import { useState } from 'react'
import { Link } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { ErrorBanner, Field, Loading, Notice } from '@/components/hub/common'
import { Key, Pill, type Tone } from '@/components/hub/pills'
import { ApiError, get } from '@/lib/api'
import { itemHref } from '@/components/hub/search'
import { fmtDate, fmtTime, today } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { CommandForm, CoordStatus, PersonLabel, SelectField, personName, workChoices, workRef, type CoordOptions, type WorkRef } from './CoordinationForms'

type Assessment = { id: string; rowVersion: number; ownerId: string; state: string; intendedOutput: string; completionCriteria: string; evaluatedAt: string }
type Check = { id: string; rowVersion: number; code: string; applies: boolean | null; satisfied: boolean | null; reason?: string; evidenceUrl?: string; recordedBy?: string }
type RdyException = { id: string; basisVersionId: string; approvedBy: string; verifierId: string; limitedWork: string; risk: string; expiresOn: string; createdAt: string }
type Detail = { assessment: Assessment; checks: Check[]; unknown: string[]; blocked: string[]; exceptions: RdyException[]; sources: { code: string; record: LinkedRecord }[] }
type BasisVersion = { id: string; number: number; status: string; scope: string; rowVersion: number }
type BasisDetail = { entry: { key: string; title: string }; versions: { version: BasisVersion }[];
  uses: { versionId: string; targetType: string; targetId: string; isCurrent: boolean }[];
  dispositions: { versionId: string; ownerId: string; approvedBy: string; scope: string; expiresOn: string }[] }
type Assumption = { key: string; title: string; version: BasisVersion; eligible: boolean; disposedUntil?: string }
type LinkedRecord = { type: string; id: string; key: string; title: string; status: string }
type Constraint = { id: string; key: string; rowVersion: number; category: string; description: string; removalOwnerId: string; affectedOwnerId: string;
  neededBy: string; sourceUrl: string; state: string; resolutionEvidenceUrl?: string; verifiedBy?: string; verifiedAt?: string
  linkedType?: string; linked?: LinkedRecord | null }
const LINK_TYPES = ['Decision', 'Issue', 'Handoff']
type PackageRef = { id: string; key: string; title: string; status: string }
type Prerequisite = { id: string; rowVersion: number; packageId: string; reason: string; createdAt: string; createdBy?: string; removedAt?: string
  removedBy?: string; removalReason?: string; package: PackageRef; effective: PackageRef | null; listsOutput: boolean }
const card = 'space-y-2 rounded-lg border p-4', subhead = 'text-base/6 font-semibold', link = 'text-primary underline underline-offset-4'
const dl = 'grid grid-cols-[minmax(8rem,auto)_1fr] gap-x-4 gap-y-2'
/** An unassessed answer stays visibly unknown (▲); it never reads as satisfied. */
const answer = (value: boolean | null, yes: string, no: string, noTone: Tone) =>
  value === null ? <Pill tone="warn">{t('readiness.unassessed')}</Pill> : <Pill tone={value ? 'done' : noTone}>{t(value ? yes : no)}</Pill>

export function ReadinessInspector({ projectId, number, options, initial, close, done }: { projectId: string; number: string; options: CoordOptions
  initial?: string; close: () => void; done: () => void }) {
  const [target, setTarget] = useState(initial ?? ''), [creating, setCreating] = useState(false), [editing, setEditing] = useState<Check | null>(null)
  const [raising, setRaising] = useState(false), [moving, setMoving] = useState<{ row: Constraint; state: string } | null>(null)
  const [type, id] = target.split(':'), work = workRef(options, type, id)
  const path = `projects/${projectId}/readiness/${type}/${id}`
  const q = useQuery({ queryKey: ['readiness-detail', projectId, type, id], enabled: !!work,
    queryFn: async () => { try { return await get<Detail>(path) } catch (e) { if (e instanceof ApiError && e.status === 404) return null; throw e } } })
  const constraints = useQuery({ queryKey: ['readiness-constraints', projectId, type, id], enabled: !!work,
    queryFn: () => get<Constraint[]>(`${path}/constraints`) })
  const [excepting, setExcepting] = useState(false), [linking, setLinking] = useState(false), [unlinking, setUnlinking] = useState<Prerequisite | null>(null)
  const prerequisites = useQuery({ queryKey: ['readiness-prerequisites', projectId, type, id], enabled: !!work,
    queryFn: () => get<Prerequisite[]>(`${path}/submission-prerequisites`) })
  // The exception references a basis version; resolve keys, scope and eligibility from the design-basis register.
  const assumptions = useQuery({ queryKey: ['readiness-assumptions', projectId, type, id], enabled: !!q.data,
    queryFn: async () => {
      const base = `projects/${projectId}/design-basis`
      const page = await get<{ items: { id: string }[] }>(`${base}?kind=Assumption&affectedWorkId=${id}&pageSize=200`)
      const details = await Promise.all(page.items.map(e => get<BasisDetail>(`${base}/${e.id}`)))
      return details.flatMap(d => d.versions.map(({ version }): Assumption => {
        const disposedUntil = d.dispositions.filter(x => x.versionId === version.id && x.ownerId === work?.ownerId && x.approvedBy !== work?.ownerId &&
          x.scope.toLowerCase() === version.scope.toLowerCase()).map(x => x.expiresOn).sort().pop()
        const current = d.uses.some(u => u.isCurrent && u.versionId === version.id && u.targetType === type && u.targetId === id)
        return { key: d.entry.key, title: d.entry.title, version, disposedUntil, eligible: current && version.status === 'Proposed' && !!disposedUntil && disposedUntil >= today() }
      }))
    } })
  const changed = () => { setCreating(false); setEditing(null); setRaising(false); setMoving(null); setExcepting(false); setLinking(false); setUnlinking(null)
    q.refetch(); constraints.refetch(); assumptions.refetch(); prerequisites.refetch(); done() }
  const canCreate = options.canWrite && work?.ownerId === options.actorId
  const canAssess = options.canWrite && work && options.manageDisciplineIds.includes(work.projectDisciplineId)
  if (creating && work) return <CreateAssessment path={path} rowVersion={work.rowVersion} assessmentVersion={q.data?.assessment.rowVersion} close={() => setCreating(false)} done={changed} />
  if (editing && q.data) return <Applicability path={`${path}/checks/${encodeURIComponent(editing.code)}/applicability`} check={editing}
    assessmentVersion={q.data.assessment.rowVersion} close={() => setEditing(null)} done={changed} />
  if (raising && work) return <RaiseConstraint path={`${path}/constraints`} projectId={projectId} work={work} options={options} close={() => setRaising(false)} done={changed} />
  if (excepting && q.data && work) return <ApproveException path={`${path}/exceptions`} assessment={q.data.assessment} work={work} options={options}
    candidates={assumptions.data?.filter(a => a.eligible) ?? []} close={() => setExcepting(false)} done={changed} />
  if (linking && work) return <LinkPrerequisite path={`${path}/submission-prerequisites`} projectId={projectId} work={work}
    linked={prerequisites.data?.filter(l => !l.removedAt).map(l => l.packageId) ?? []} close={() => setLinking(false)} done={changed} />
  if (unlinking) return <RemovePrerequisite path={`${path}/submission-prerequisites/${unlinking.id}/remove`} row={unlinking}
    close={() => setUnlinking(null)} done={changed} />
  if (moving) return <MoveConstraint path={`${path}/constraints/${moving.row.id}/transition`} row={moving.row} state={moving.state}
    close={() => setMoving(null)} done={changed} />
  return <Dialog open onOpenChange={o => !o && close()}><DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
    <DialogHeader><DialogTitle>{t('readiness.inspect')}</DialogTitle><DialogDescription>{t('readiness.inspectHint')}</DialogDescription></DialogHeader>
    <SelectField label={t('readiness.work')} value={target} onChange={v => { setTarget(v); setEditing(null) }} choices={workChoices(options)} />
    {work && (q.isPending ? <Loading rows={4} /> : q.error ? <ErrorBanner error={q.error} retry={() => q.refetch()} /> : !q.data
      ? <Notice title={t('readiness.noAssessment')} action={canCreate && <Button onClick={() => setCreating(true)}>{t('readiness.createAssessment')}</Button>} />
      : <div className="space-y-5 text-sm">
      <div className="flex flex-wrap items-center gap-2"><CoordStatus status={q.data.assessment.state} /><span className="text-xs/[18px] text-muted-foreground">{t('readiness.evaluated')}: <span className="tabular-nums">{fmtTime(q.data.assessment.evaluatedAt)}</span></span></div>
      <dl className={dl}>
        <dt className="text-muted-foreground">{t('readiness.performer')}</dt><dd><PersonLabel options={options} id={q.data.assessment.ownerId} /></dd>
        <dt className="text-muted-foreground">{t('readiness.output')}</dt><dd className="whitespace-pre-wrap">{q.data.assessment.intendedOutput}</dd>
        <dt className="text-muted-foreground">{t('readiness.criteria')}</dt><dd className="whitespace-pre-wrap">{q.data.assessment.completionCriteria}</dd>
      </dl>
      {canCreate && q.data.assessment.ownerId !== work.ownerId && <Button size="sm" variant="outline" onClick={() => setCreating(true)}>{t('readiness.recoverAssessment')}</Button>}
      {q.data.unknown.length > 0 && <p className="flex gap-2 rounded-md border border-warn/40 bg-warn-bg px-4 py-3 text-warn"><span aria-hidden>▲</span><span>{t('readiness.unknown')}: {q.data.unknown.map(tv).join(', ')}</span></p>}
      {q.data.blocked.length > 0 && <p className="flex gap-2 rounded-md border border-bad/30 bg-bad-bg px-4 py-3 text-bad"><span aria-hidden>■</span><span>{t('readiness.blocked')}: {q.data.blocked.map(tv).join(', ')}</span></p>}
      <ul className="space-y-2">{q.data.checks.map(c => <li key={c.id} className={card}>
        <h3 className="font-semibold">{tv(c.code)}</h3>
        <dl className={dl}>
          <dt className="text-muted-foreground">{t('readiness.applicability')}</dt><dd>{answer(c.applies, 'readiness.applies', 'readiness.notApplicable', 'idle')}</dd>
          {c.applies !== false && <><dt className="text-muted-foreground">{t('readiness.result')}</dt><dd>{answer(c.satisfied, 'readiness.satisfied', 'readiness.unsatisfied', 'bad')}</dd></>}
        </dl>
        {c.reason && <p className="whitespace-pre-wrap">{c.reason}</p>}
        {(q.data!.sources ?? []).some(s => s.code === c.code) && <ul aria-label={t('readiness.sources')} className="space-y-1">
          {(q.data!.sources ?? []).filter(s => s.code === c.code).map(({ record: r }) => <li key={`${r.type}:${r.id}`} className="flex flex-wrap items-center gap-2"><Link className={link} to={itemHref(r.type, number, r.id)}><span className="key">{r.key}</span> · {r.title}</Link><CoordStatus status={r.status} /></li>)}
        </ul>}
        {c.evidenceUrl && <a className={link} href={c.evidenceUrl} target="_blank" rel="noopener noreferrer">{t('basis.evidence')}</a>}
        {c.recordedBy && <p className="text-xs/[18px] text-muted-foreground">{t('readiness.recordedBy')}: {personName(options, c.recordedBy)}</p>}
        {canAssess && <Button size="sm" variant="outline" onClick={() => setEditing(c)}>{t('readiness.recordApplicability')} · {tv(c.code)}</Button>}
      </li>)}</ul>
      <section className="space-y-3" aria-labelledby="readiness-exceptions">
        <h3 id="readiness-exceptions" className={subhead}>{t('readiness.exceptions')}</h3>
        {assumptions.error && <ErrorBanner error={assumptions.error} retry={() => assumptions.refetch()} />}
        {q.data.exceptions.length === 0 ? <p className="text-muted-foreground">{t('readiness.exceptionNone')}</p> : <ul className="space-y-2">{[...q.data.exceptions].reverse().map((x, i) => {
          const basis = assumptions.data?.find(a => a.version.id === x.basisVersionId)
          const status = x.expiresOn < today() ? 'readiness.exceptionExpired' : i === 0 && q.data!.assessment.state === 'Proceed under Assumption' ? 'readiness.exceptionActive' : 'readiness.exceptionInactive'
          return <li key={x.id} className={card}>
            <Pill tone={status === 'readiness.exceptionActive' ? 'warn' : 'idle'}>{t(status)}</Pill>
            <p>{t('readiness.exceptionBasis')}: {basis ? `${basis.key} · ${t('basis.version')} ${basis.version.number} · ${basis.title}` : t('coord.unavailable')}</p>
            {basis && <p>{t('basis.scope')}: {basis.version.scope}</p>}
            <p>{t('readiness.exceptionLimitedWork')}: {x.limitedWork}</p><p>{t('readiness.exceptionRisk')}: {x.risk}</p>
            <p>{t('basis.approvedBy')}: {personName(options, x.approvedBy)} · <span className="tabular-nums">{fmtDate(x.createdAt)}</span></p>
            <p>{t('readiness.exceptionVerifier')}: {personName(options, x.verifierId)}</p><p>{t('basis.expiry')}: <span className="tabular-nums">{fmtDate(x.expiresOn)}</span></p>
          </li>
        })}</ul>}
        {canAssess && (assumptions.isPending ? <Loading rows={1} /> : assumptions.data?.some(a => a.eligible)
          ? <Button size="sm" variant="outline" onClick={() => setExcepting(true)}>{t('readiness.exceptionApprove')}</Button>
          : !assumptions.error && <p className="text-xs/[18px] text-muted-foreground">{t('readiness.exceptionIneligible')}</p>)}
      </section>
    </div>)}
    {work && <section className="space-y-3 text-sm" aria-label={t('readiness.constraints')}>
      <div className="flex flex-wrap items-center justify-between gap-2"><h3 className={subhead}>{t('readiness.constraints')}</h3>
        {(canCreate || canAssess) && <Button size="sm" variant="outline" onClick={() => setRaising(true)}>{t('readiness.raiseConstraint')}</Button>}</div>
      {constraints.isPending ? <Loading rows={3} /> : constraints.error ? <ErrorBanner error={constraints.error} retry={() => constraints.refetch()} /> : constraints.data?.length === 0 ? <p className="text-muted-foreground">{t('readiness.noWorkConstraints')}</p> :
        <ul className="space-y-2">{constraints.data?.map(c => <li key={c.id} className={card}>
          <div className="flex flex-wrap items-center gap-2"><Key>{c.key}</Key><CoordStatus status={c.state} /></div>
          <p className="font-medium">{c.description}</p><p className="text-muted-foreground">{tv(c.category)} · <span className="tabular-nums">{fmtDate(c.neededBy)}</span></p>
          {c.linkedType && <p className="flex flex-wrap items-center gap-2">{t('readiness.linkedRecord')}: {c.linked
            ? <><Link className={link} to={itemHref(c.linked.type, number, c.linked.id)}>{tv(c.linked.type)} <span className="key">{c.linked.key}</span> · {c.linked.title}</Link><CoordStatus status={c.linked.status} /></>
            : <span className="text-muted-foreground">{t('coord.unavailable')}</span>}</p>}
          <dl className={dl}>
            <dt className="text-muted-foreground">{t('readiness.removalOwner')}</dt><dd><PersonLabel options={options} id={c.removalOwnerId} /></dd>
            <dt className="text-muted-foreground">{t('readiness.affectedOwner')}</dt><dd><PersonLabel options={options} id={c.affectedOwnerId} /></dd>
          </dl>
          <a className={link} href={c.sourceUrl} target="_blank" rel="noopener noreferrer">{t('readiness.constraintSource')}</a>
          {c.resolutionEvidenceUrl && <p><a className={link} href={c.resolutionEvidenceUrl} target="_blank" rel="noopener noreferrer">{t('basis.evidence')}</a></p>}
          {c.verifiedBy && <p>{t('readiness.verifiedBy')}: {personName(options, c.verifiedBy)} · <span className="tabular-nums">{fmtTime(c.verifiedAt)}</span></p>}
          <div className="flex flex-wrap gap-2">
            {options.canWrite && c.state === 'Open' && c.removalOwnerId === options.actorId && <Button size="sm" onClick={() => setMoving({ row: c, state: 'Resolution Proposed' })}>{t('readiness.proposeResolution')}</Button>}
            {options.canWrite && c.state === 'Resolution Proposed' && c.affectedOwnerId === options.actorId && work.ownerId === c.affectedOwnerId && <Button size="sm" onClick={() => setMoving({ row: c, state: 'Verified Removed' })}>{t('readiness.verifyRemoval')}</Button>}
            {canAssess && ['Open', 'Resolution Proposed'].includes(c.state) && <Button size="sm" variant="ghost" className="text-bad hover:text-bad" onClick={() => setMoving({ row: c, state: 'Cancelled' })}>{t('readiness.cancelConstraint')}</Button>}
          </div>
        </li>)}</ul>}
    </section>}
    {work && <section className="space-y-3 text-sm" aria-labelledby="readiness-prerequisites">
      <div className="flex flex-wrap items-center justify-between gap-2"><h3 id="readiness-prerequisites" className={subhead}>{t('readiness.prerequisites')}</h3>
        {canAssess && <Button size="sm" variant="outline" onClick={() => setLinking(true)}>{t('readiness.addPrerequisite')}</Button>}</div>
      <p className="text-xs/[18px] text-muted-foreground">{t('readiness.prerequisitesHint')}</p>
      {prerequisites.isPending ? <Loading rows={2} /> : prerequisites.error ? <ErrorBanner error={prerequisites.error} retry={() => prerequisites.refetch()} /> :
        prerequisites.data?.length === 0 ? <p className="text-muted-foreground">{t('readiness.noPrerequisites')}</p> :
        <ul className="space-y-2">{prerequisites.data?.map(l => <li key={l.id} className={card}>
          <p className="font-medium"><Link className={link} to={`/projects/${number}/submissions?panel=SubmissionPackage:${l.packageId}`}><span className="key">{l.package.key}</span> · {l.package.title}</Link></p>
          <p className="flex flex-wrap items-center gap-2"><CoordStatus status={l.package.status} />
            {l.effective && l.effective.id !== l.package.id && <>{t('readiness.prerequisiteCurrent')}: <span className="key">{l.effective.key}</span> <CoordStatus status={l.effective.status} /></>}</p>
          {l.listsOutput && <p role="status" className="flex gap-2 text-warn"><span aria-hidden>▲</span><span>{t('readiness.prerequisiteListsOutput')}</span></p>}
          <p className="whitespace-pre-wrap">{l.reason}</p>
          <p className="text-xs/[18px] text-muted-foreground">{t('readiness.linkedBy')}: {personName(options, l.createdBy)} · <span className="tabular-nums">{fmtDate(l.createdAt)}</span></p>
          {l.removedAt ? <p>{t('readiness.prerequisiteRemoved')}: {personName(options, l.removedBy)} · <span className="tabular-nums">{fmtDate(l.removedAt)}</span> · {l.removalReason}</p>
            : canAssess && <Button size="sm" variant="outline" onClick={() => setUnlinking(l)}>{t('readiness.removePrerequisite')}</Button>}
        </li>)}</ul>}
    </section>}
  </DialogContent></Dialog>
}

function LinkPrerequisite({ path, projectId, work, linked, close, done }: { path: string; projectId: string; work: WorkRef; linked: string[]; close: () => void; done: () => void }) {
  const packages = useQuery({ queryKey: ['submission-choices', projectId], queryFn: () => get<{ items: PackageRef[] }>(`projects/${projectId}/submissions?pageSize=200`) })
  const [packageId, setPackage] = useState(''), [reason, setReason] = useState('')
  return <CommandForm path={path} title={t('readiness.addPrerequisite')} hint={t('readiness.prerequisitesHint')} onClose={close} onDone={done}
    submitLabel={t('readiness.addPrerequisite')} payload={() => ({ targetRowVersion: work.rowVersion, packageId, reason })}>
    {packages.isPending ? <Loading rows={1} /> : packages.error ? <ErrorBanner error={packages.error} retry={() => packages.refetch()} /> :
      <SelectField label={t('readiness.prerequisitePackage')} value={packageId} onChange={setPackage}
        choices={packages.data.items.filter(p => !linked.includes(p.id)).map(p => ({ value: p.id, label: `${p.key} · ${p.title} · ${tv(p.status)}` }))} />}
    <Field label={t('basis.reason')} htmlFor="prerequisite-reason"><Textarea id="prerequisite-reason" required minLength={5} maxLength={4000} value={reason} onChange={e => setReason(e.target.value)} /></Field>
  </CommandForm>
}

function RemovePrerequisite({ path, row, close, done }: { path: string; row: Prerequisite; close: () => void; done: () => void }) {
  const [reason, setReason] = useState('')
  return <CommandForm path={path} title={t('readiness.removePrerequisite')} hint={t('readiness.prerequisitesHint')} onClose={close} onDone={done}
    submitLabel={t('readiness.removePrerequisite')} payload={() => ({ rowVersion: row.rowVersion, reason })}>
    <p className="rounded-md bg-muted px-4 py-3 text-sm font-medium"><span className="key">{row.package.key}</span> · {row.package.title}</p>
    <Field label={t('basis.reason')} htmlFor="prerequisite-remove-reason"><Textarea id="prerequisite-remove-reason" required minLength={5} maxLength={4000} value={reason} onChange={e => setReason(e.target.value)} /></Field>
  </CommandForm>
}

function RaiseConstraint({ path, projectId, work, options, close, done }: { path: string; projectId: string; work: WorkRef; options: CoordOptions; close: () => void; done: () => void }) {
  const [category, setCategory] = useState(''), [description, setDescription] = useState(''), [removalOwnerId, setOwner] = useState(''), [neededBy, setNeeded] = useState(today()), [sourceUrl, setSource] = useState('')
  const [linkedType, setLinkedType] = useState(''), [linkedId, setLinkedId] = useState('')
  const records = useQuery({ queryKey: ['readiness-link-options', projectId, linkedType], enabled: !!linkedType,
    queryFn: () => get<LinkedRecord[]>(`projects/${projectId}/readiness/link-options?type=${linkedType}`) })
  return <CommandForm path={path} title={t('readiness.raiseConstraint')} hint={t('readiness.constraintHint')} onClose={close} onDone={done}
    payload={() => ({ targetRowVersion: work.rowVersion, category, description, removalOwnerId, neededBy, sourceUrl,
      linkedType: linkedType || null, linkedId: linkedType ? linkedId : null })}>
    <SelectField label={t('readiness.category')} value={category} onChange={setCategory} choices={['Handoff', 'Decision', 'Basis', 'Capacity', 'Review', 'Scope', 'Other'].map(v => ({ value: v, label: tv(v) }))} />
    <Field label={t('readiness.constraintDescription')} htmlFor="constraint-description"><Textarea id="constraint-description" required maxLength={2000} value={description} onChange={e => setDescription(e.target.value)} /></Field>
    <div className="grid gap-3 sm:grid-cols-2">
      <SelectField label={t('readiness.removalOwner')} value={removalOwnerId} onChange={setOwner} choices={options.people.filter(p => p.id !== work.ownerId).map(p => ({ value: p.id, label: p.displayName }))} />
      <Field label={t('readiness.neededBy')} htmlFor="constraint-needed"><Input id="constraint-needed" required type="date" value={neededBy} onChange={e => setNeeded(e.target.value)} /></Field>
    </div>
    <Field label={t('readiness.constraintSource')} htmlFor="constraint-source"><Input id="constraint-source" required type="url" value={sourceUrl} onChange={e => setSource(e.target.value)} /></Field>
    <SelectField label={t('readiness.linkedType')} value={linkedType} onChange={v => { setLinkedType(v); setLinkedId('') }} required={false}
      choices={LINK_TYPES.map(v => ({ value: v, label: tv(v) }))} />
    {linkedType && (records.isPending ? <Loading rows={1} /> : records.error ? <ErrorBanner error={records.error} retry={() => records.refetch()} />
      : <SelectField label={t('readiness.linkedRecord')} value={linkedId} onChange={setLinkedId}
        choices={(records.data ?? []).map(r => ({ value: r.id, label: `${r.key} · ${r.title} · ${tv(r.status)}` }))} />)}
  </CommandForm>
}

function MoveConstraint({ path, row, state, close, done }: { path: string; row: Constraint; state: string; close: () => void; done: () => void }) {
  const [reason, setReason] = useState(''), [evidenceUrl, setEvidence] = useState('')
  const title = t(state === 'Resolution Proposed' ? 'readiness.proposeResolution' : state === 'Verified Removed' ? 'readiness.verifyRemoval' : 'readiness.cancelConstraint')
  return <CommandForm path={path} title={title} hint={t('readiness.resolutionHint')} onClose={close} onDone={done} submitLabel={title}
    payload={() => ({ rowVersion: row.rowVersion, toState: state, reason, evidenceUrl: evidenceUrl || null })}>
    <div className="space-y-1 rounded-md bg-muted px-4 py-3 text-sm"><p className="whitespace-pre-wrap">{row.description}</p>{row.resolutionEvidenceUrl && <a className={link} href={row.resolutionEvidenceUrl} target="_blank" rel="noopener noreferrer">{t('basis.evidence')}</a>}</div>
    <Field label={t('basis.reason')} htmlFor="constraint-reason"><Textarea id="constraint-reason" required minLength={5} value={reason} onChange={e => setReason(e.target.value)} /></Field>
    {state === 'Resolution Proposed' && <Field label={t('basis.evidence')} htmlFor="constraint-evidence"><Input id="constraint-evidence" required type="url" value={evidenceUrl} onChange={e => setEvidence(e.target.value)} /></Field>}
  </CommandForm>
}

function CreateAssessment({ path, rowVersion, assessmentVersion, close, done }: { path: string; rowVersion: number; assessmentVersion?: number; close: () => void; done: () => void }) {
  const [output, setOutput] = useState(''), [criteria, setCriteria] = useState(''), [reason, setReason] = useState('')
  return <CommandForm path={path} title={t(assessmentVersion != null ? 'readiness.recoverAssessment' : 'readiness.createAssessment')} hint={t(assessmentVersion != null ? 'readiness.recoverHint' : 'readiness.createHint')} onClose={close} onDone={done}
    payload={() => ({ targetRowVersion: rowVersion, intendedOutput: output, completionCriteria: criteria, reason: reason || null, assessmentRowVersion: assessmentVersion })}>
    <Field label={t('readiness.output')} htmlFor="assessment-output"><Textarea id="assessment-output" required maxLength={2000} value={output} onChange={e => setOutput(e.target.value)} /></Field>
    <Field label={t('readiness.criteria')} htmlFor="assessment-criteria"><Textarea id="assessment-criteria" required maxLength={2000} value={criteria} onChange={e => setCriteria(e.target.value)} /></Field>
    {assessmentVersion != null && <Field label={t('basis.reason')} htmlFor="assessment-recovery-reason"><Textarea id="assessment-recovery-reason" required minLength={5} maxLength={4000} value={reason} onChange={e => setReason(e.target.value)} /></Field>}
  </CommandForm>
}

function Applicability({ path, check, assessmentVersion, close, done }: { path: string; check: Check; assessmentVersion: number; close: () => void; done: () => void }) {
  const [applies, setApplies] = useState(check.applies === null ? '' : String(check.applies)), [reason, setReason] = useState(''), [evidenceUrl, setEvidenceUrl] = useState('')
  return <CommandForm path={path} title={`${t('readiness.recordApplicability')} · ${tv(check.code)}`} hint={t('readiness.applicabilityHint')}
    onClose={close} onDone={done} payload={() => ({ assessmentRowVersion: assessmentVersion, checkRowVersion: check.rowVersion, applies: applies === 'true', reason, evidenceUrl: evidenceUrl || null })}>
    <SelectField label={t('readiness.applicability')} value={applies} onChange={setApplies} choices={[
      { value: 'true', label: t('readiness.applies') }, ...(check.code === 'Production Owner' ? [] : [{ value: 'false', label: t('readiness.notApplicable') }])]} />
    <Field label={t('basis.reason')} htmlFor="applicability-reason"><Textarea id="applicability-reason" required minLength={5} value={reason} onChange={e => setReason(e.target.value)} /></Field>
    <Field label={t('basis.evidence')} htmlFor="applicability-evidence" optional><Input id="applicability-evidence" type="url" value={evidenceUrl} onChange={e => setEvidenceUrl(e.target.value)} /></Field>
  </CommandForm>
}

function ApproveException({ path, assessment, work, options, candidates, close, done }: { path: string; assessment: Assessment; work: WorkRef;
  options: CoordOptions; candidates: Assumption[]; close: () => void; done: () => void }) {
  const [versionId, setVersion] = useState(candidates.length === 1 ? candidates[0].version.id : ''), [verifierId, setVerifier] = useState('')
  const [limitedWork, setLimited] = useState(''), [risk, setRisk] = useState(''), [expiresOn, setExpiry] = useState('')
  const chosen = candidates.find(c => c.version.id === versionId)
  return <CommandForm path={path} title={t('readiness.exceptionApprove')} hint={t('readiness.exceptionHint')} onClose={close} onDone={done}
    submitLabel={t('readiness.exceptionApprove')}
    payload={() => ({ assessmentRowVersion: assessment.rowVersion, basisVersionId: versionId, basisVersionRowVersion: chosen?.version.rowVersion ?? 0,
      verifierId, limitedWork, risk, expiresOn })}>
    <SelectField label={t('readiness.exceptionBasis')} value={versionId} onChange={setVersion}
      choices={candidates.map(c => ({ value: c.version.id, label: `${c.key} · ${t('basis.version')} ${c.version.number} · ${c.title}` }))} />
    {chosen && <p className="rounded-md bg-muted px-4 py-3 text-sm">{t('basis.scope')}: {chosen.version.scope} · {t('readiness.exceptionDisposedUntil', { date: fmtDate(chosen.disposedUntil) })}</p>}
    <Field label={t('readiness.exceptionLimitedWork')} htmlFor="exception-limited"><Textarea id="exception-limited" required maxLength={2000} value={limitedWork} onChange={e => setLimited(e.target.value)} /></Field>
    <Field label={t('readiness.exceptionRisk')} htmlFor="exception-risk"><Textarea id="exception-risk" required maxLength={2000} value={risk} onChange={e => setRisk(e.target.value)} /></Field>
    <div className="grid gap-3 sm:grid-cols-2">
      <SelectField label={t('readiness.exceptionVerifier')} value={verifierId} onChange={setVerifier}
        choices={options.people.filter(p => p.id !== work.ownerId && p.id !== options.actorId).map(p => ({ value: p.id, label: p.displayName }))} />
      <Field label={t('basis.expiry')} htmlFor="exception-expiry"><Input id="exception-expiry" required type="date" min={today()} max={chosen?.disposedUntil}
        value={expiresOn} onChange={e => setExpiry(e.target.value)} /></Field>
    </div>
  </CommandForm>
}
