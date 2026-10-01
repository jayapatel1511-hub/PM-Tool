import { useState } from 'react'
import { Link } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { ErrorBanner, Field, Loading } from '@/components/hub/common'
import { StatusPill } from '@/components/hub/pills'
import { ApiError, get } from '@/lib/api'
import { itemHref } from '@/components/hub/search'
import { fmtDate, fmtTime, today } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { CommandForm, SelectField, personName, workChoices, workRef, type CoordOptions, type WorkRef } from './CoordinationForms'

type Assessment = { id: string; rowVersion: number; ownerId: string; state: string; intendedOutput: string; completionCriteria: string; evaluatedAt: string }
type Check = { id: string; rowVersion: number; code: string; applies: boolean | null; satisfied: boolean | null; reason?: string; evidenceUrl?: string; recordedBy?: string }
type RdyException = { id: string; basisVersionId: string; approvedBy: string; verifierId: string; limitedWork: string; risk: string; expiresOn: string; createdAt: string }
type Detail = { assessment: Assessment; checks: Check[]; unknown: string[]; blocked: string[]; exceptions: RdyException[] }
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

export function ReadinessInspector({ projectId, number, options, close, done }: { projectId: string; number: string; options: CoordOptions; close: () => void; done: () => void }) {
  const [target, setTarget] = useState(''), [creating, setCreating] = useState(false), [editing, setEditing] = useState<Check | null>(null)
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
  if (creating && work) return <CreateAssessment path={path} rowVersion={work.rowVersion} close={() => setCreating(false)} done={changed} />
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
    {work && (q.isPending ? <Loading rows={4} /> : q.error ? <ErrorBanner error={q.error} retry={() => q.refetch()} /> : !q.data ? <div className="space-y-3">
      <p>{t('readiness.noAssessment')}</p>{canCreate && <Button onClick={() => setCreating(true)}>{t('readiness.createAssessment')}</Button>}
    </div> : <div className="space-y-4 text-sm">
      <StatusPill status={q.data.assessment.state} />
      <p>{t('readiness.performer')}: {personName(options, q.data.assessment.ownerId)}</p>
      <p>{t('readiness.output')}: {q.data.assessment.intendedOutput}</p><p>{t('readiness.criteria')}: {q.data.assessment.completionCriteria}</p>
      <p className="text-xs text-muted-foreground">{t('readiness.evaluated')}: {fmtDate(q.data.assessment.evaluatedAt)} {fmtTime(q.data.assessment.evaluatedAt)}</p>
      {q.data.unknown.length > 0 && <p>{t('readiness.unknown')}: {q.data.unknown.map(tv).join(', ')}</p>}
      {q.data.blocked.length > 0 && <p>{t('readiness.blocked')}: {q.data.blocked.map(tv).join(', ')}</p>}
      <ul className="space-y-2">{q.data.checks.map(c => <li key={c.id} className="space-y-2 rounded border p-3">
        <h3 className="font-medium">{tv(c.code)}</h3>
        <p>{t('readiness.applicability')}: {t(c.applies === null ? 'readiness.unassessed' : c.applies ? 'readiness.applies' : 'readiness.notApplicable')}</p>
        {c.applies !== false && <p>{t('readiness.result')}: {t(c.satisfied === null ? 'readiness.unassessed' : c.satisfied ? 'readiness.satisfied' : 'readiness.unsatisfied')}</p>}
        {c.reason && <p>{c.reason}</p>}
        {c.evidenceUrl && <a className="text-primary underline" href={c.evidenceUrl} target="_blank" rel="noopener noreferrer">{t('basis.evidence')}</a>}
        {c.recordedBy && <p className="text-xs text-muted-foreground">{t('readiness.recordedBy')}: {personName(options, c.recordedBy)}</p>}
        {canAssess && <Button size="sm" variant="outline" onClick={() => setEditing(c)}>{t('readiness.recordApplicability')} · {tv(c.code)}</Button>}
      </li>)}</ul>
      <section className="space-y-3" aria-labelledby="readiness-exceptions">
        <h3 id="readiness-exceptions" className="font-medium">{t('readiness.exceptions')}</h3>
        {assumptions.error && <ErrorBanner error={assumptions.error} retry={() => assumptions.refetch()} />}
        {q.data.exceptions.length === 0 ? <p>{t('readiness.exceptionNone')}</p> : <ul className="space-y-2">{[...q.data.exceptions].reverse().map((x, i) => {
          const basis = assumptions.data?.find(a => a.version.id === x.basisVersionId)
          const status = x.expiresOn < today() ? 'readiness.exceptionExpired' : i === 0 && q.data!.assessment.state === 'Proceed under Assumption' ? 'readiness.exceptionActive' : 'readiness.exceptionInactive'
          return <li key={x.id} className="space-y-1 rounded border p-3">
            <p className="font-medium">{t(status)}</p>
            <p>{t('readiness.exceptionBasis')}: {basis ? `${basis.key} · ${t('basis.version')} ${basis.version.number} · ${basis.title}` : t('coord.unavailable')}</p>
            {basis && <p>{t('basis.scope')}: {basis.version.scope}</p>}
            <p>{t('readiness.exceptionLimitedWork')}: {x.limitedWork}</p><p>{t('readiness.exceptionRisk')}: {x.risk}</p>
            <p>{t('basis.approvedBy')}: {personName(options, x.approvedBy)} · {fmtDate(x.createdAt)}</p>
            <p>{t('readiness.exceptionVerifier')}: {personName(options, x.verifierId)}</p><p>{t('basis.expiry')}: {fmtDate(x.expiresOn)}</p>
          </li>
        })}</ul>}
        {canAssess && (assumptions.isPending ? <Loading rows={1} /> : assumptions.data?.some(a => a.eligible)
          ? <Button size="sm" variant="outline" onClick={() => setExcepting(true)}>{t('readiness.exceptionApprove')}</Button>
          : !assumptions.error && <p className="text-xs text-muted-foreground">{t('readiness.exceptionIneligible')}</p>)}
      </section>
    </div>)}
    {work && <section className="space-y-3 text-sm" aria-label={t('readiness.constraints')}>
      <h3 className="font-medium">{t('readiness.constraints')}</h3>
      {(canCreate || canAssess) && <Button size="sm" variant="outline" onClick={() => setRaising(true)}>{t('readiness.raiseConstraint')}</Button>}
      {constraints.isPending ? <Loading rows={3} /> : constraints.error ? <ErrorBanner error={constraints.error} retry={() => constraints.refetch()} /> : constraints.data?.length === 0 ? <p>{t('readiness.noWorkConstraints')}</p> :
        <ul className="space-y-2">{constraints.data?.map(c => <li key={c.id} className="space-y-2 rounded border p-3">
          <StatusPill status={c.state} /><p className="font-medium"><span className="mr-2 font-mono text-xs text-muted-foreground">{c.key}</span>{c.description}</p><p>{tv(c.category)} · {fmtDate(c.neededBy)}</p>
          {c.linkedType && <p className="flex flex-wrap items-center gap-2">{t('readiness.linkedRecord')}: {c.linked
            ? <><Link className="text-primary underline" to={itemHref(c.linked.type, number, c.linked.id)}>{tv(c.linked.type)} {c.linked.key} · {c.linked.title}</Link><StatusPill status={c.linked.status} /></>
            : t('coord.unavailable')}</p>}
          <p>{t('readiness.removalOwner')}: {personName(options, c.removalOwnerId)}</p><p>{t('readiness.affectedOwner')}: {personName(options, c.affectedOwnerId)}</p>
          <a className="text-primary underline" href={c.sourceUrl} target="_blank" rel="noopener noreferrer">{t('readiness.constraintSource')}</a>
          {c.resolutionEvidenceUrl && <p><a className="text-primary underline" href={c.resolutionEvidenceUrl} target="_blank" rel="noopener noreferrer">{t('basis.evidence')}</a></p>}
          {c.verifiedBy && <p>{t('readiness.verifiedBy')}: {personName(options, c.verifiedBy)} · {fmtDate(c.verifiedAt)} {fmtTime(c.verifiedAt)}</p>}
          <div className="flex flex-wrap gap-2">
            {options.canWrite && c.state === 'Open' && c.removalOwnerId === options.actorId && <Button size="sm" onClick={() => setMoving({ row: c, state: 'Resolution Proposed' })}>{t('readiness.proposeResolution')}</Button>}
            {options.canWrite && c.state === 'Resolution Proposed' && c.affectedOwnerId === options.actorId && work.ownerId === c.affectedOwnerId && <Button size="sm" onClick={() => setMoving({ row: c, state: 'Verified Removed' })}>{t('readiness.verifyRemoval')}</Button>}
            {canAssess && ['Open', 'Resolution Proposed'].includes(c.state) && <Button size="sm" variant="outline" onClick={() => setMoving({ row: c, state: 'Cancelled' })}>{t('readiness.cancelConstraint')}</Button>}
          </div>
        </li>)}</ul>}
    </section>}
    {work && <section className="space-y-3 text-sm" aria-labelledby="readiness-prerequisites">
      <h3 id="readiness-prerequisites" className="font-medium">{t('readiness.prerequisites')}</h3>
      <p className="text-xs text-muted-foreground">{t('readiness.prerequisitesHint')}</p>
      {canAssess && <Button size="sm" variant="outline" onClick={() => setLinking(true)}>{t('readiness.addPrerequisite')}</Button>}
      {prerequisites.isPending ? <Loading rows={2} /> : prerequisites.error ? <ErrorBanner error={prerequisites.error} retry={() => prerequisites.refetch()} /> :
        prerequisites.data?.length === 0 ? <p>{t('readiness.noPrerequisites')}</p> :
        <ul className="space-y-2">{prerequisites.data?.map(l => <li key={l.id} className="space-y-1 rounded border p-3">
          <p className="font-medium"><Link className="text-primary underline" to={`/projects/${number}/submissions?panel=SubmissionPackage:${l.packageId}`}>{l.package.key} · {l.package.title}</Link></p>
          <p className="flex flex-wrap items-center gap-2"><StatusPill status={l.package.status} />
            {l.effective && l.effective.id !== l.package.id && <>{t('readiness.prerequisiteCurrent')}: {l.effective.key} <StatusPill status={l.effective.status} /></>}</p>
          {l.listsOutput && <p role="status" className="text-warn">{t('readiness.prerequisiteListsOutput')}</p>}
          <p>{l.reason}</p>
          <p className="text-xs text-muted-foreground">{t('readiness.linkedBy')}: {personName(options, l.createdBy)} · {fmtDate(l.createdAt)}</p>
          {l.removedAt ? <p>{t('readiness.prerequisiteRemoved')}: {personName(options, l.removedBy)} · {fmtDate(l.removedAt)} · {l.removalReason}</p>
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
    <p>{row.package.key} · {row.package.title}</p>
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
    <SelectField label={t('readiness.removalOwner')} value={removalOwnerId} onChange={setOwner} choices={options.people.filter(p => p.id !== work.ownerId).map(p => ({ value: p.id, label: p.displayName }))} />
    <Field label={t('readiness.neededBy')} htmlFor="constraint-needed"><Input id="constraint-needed" required type="date" value={neededBy} onChange={e => setNeeded(e.target.value)} /></Field>
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
    <p>{row.description}</p>{row.resolutionEvidenceUrl && <a className="text-primary underline" href={row.resolutionEvidenceUrl} target="_blank" rel="noopener noreferrer">{t('basis.evidence')}</a>}
    <Field label={t('basis.reason')} htmlFor="constraint-reason"><Textarea id="constraint-reason" required minLength={5} value={reason} onChange={e => setReason(e.target.value)} /></Field>
    {state === 'Resolution Proposed' && <Field label={t('basis.evidence')} htmlFor="constraint-evidence"><Input id="constraint-evidence" required type="url" value={evidenceUrl} onChange={e => setEvidence(e.target.value)} /></Field>}
  </CommandForm>
}

function CreateAssessment({ path, rowVersion, close, done }: { path: string; rowVersion: number; close: () => void; done: () => void }) {
  const [output, setOutput] = useState(''), [criteria, setCriteria] = useState('')
  return <CommandForm path={path} title={t('readiness.createAssessment')} hint={t('readiness.createHint')} onClose={close} onDone={done}
    payload={() => ({ targetRowVersion: rowVersion, intendedOutput: output, completionCriteria: criteria })}>
    <Field label={t('readiness.output')} htmlFor="assessment-output"><Textarea id="assessment-output" required maxLength={2000} value={output} onChange={e => setOutput(e.target.value)} /></Field>
    <Field label={t('readiness.criteria')} htmlFor="assessment-criteria"><Textarea id="assessment-criteria" required maxLength={2000} value={criteria} onChange={e => setCriteria(e.target.value)} /></Field>
  </CommandForm>
}

function Applicability({ path, check, assessmentVersion, close, done }: { path: string; check: Check; assessmentVersion: number; close: () => void; done: () => void }) {
  const [applies, setApplies] = useState(check.applies === null ? '' : String(check.applies)), [reason, setReason] = useState(''), [evidenceUrl, setEvidenceUrl] = useState('')
  return <CommandForm path={path} title={`${t('readiness.recordApplicability')} · ${tv(check.code)}`} hint={t('readiness.applicabilityHint')}
    onClose={close} onDone={done} payload={() => ({ assessmentRowVersion: assessmentVersion, checkRowVersion: check.rowVersion, applies: applies === 'true', reason, evidenceUrl: evidenceUrl || null })}>
    <SelectField label={t('readiness.applicability')} value={applies} onChange={setApplies} choices={[
      { value: 'true', label: t('readiness.applies') }, ...(check.code === 'Production Owner' ? [] : [{ value: 'false', label: t('readiness.notApplicable') }])]} />
    <Field label={t('basis.reason')} htmlFor="applicability-reason"><Textarea id="applicability-reason" required minLength={5} value={reason} onChange={e => setReason(e.target.value)} /></Field>
    <Field label={t('basis.evidence')} htmlFor="applicability-evidence"><Input id="applicability-evidence" type="url" value={evidenceUrl} onChange={e => setEvidenceUrl(e.target.value)} /></Field>
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
    {chosen && <p>{t('basis.scope')}: {chosen.version.scope} · {t('readiness.exceptionDisposedUntil', { date: fmtDate(chosen.disposedUntil) })}</p>}
    <Field label={t('readiness.exceptionLimitedWork')} htmlFor="exception-limited"><Textarea id="exception-limited" required maxLength={2000} value={limitedWork} onChange={e => setLimited(e.target.value)} /></Field>
    <Field label={t('readiness.exceptionRisk')} htmlFor="exception-risk"><Textarea id="exception-risk" required maxLength={2000} value={risk} onChange={e => setRisk(e.target.value)} /></Field>
    <SelectField label={t('readiness.exceptionVerifier')} value={verifierId} onChange={setVerifier}
      choices={options.people.filter(p => p.id !== work.ownerId && p.id !== options.actorId).map(p => ({ value: p.id, label: p.displayName }))} />
    <Field label={t('basis.expiry')} htmlFor="exception-expiry"><Input id="exception-expiry" required type="date" min={today()} max={chosen?.disposedUntil}
      value={expiresOn} onChange={e => setExpiry(e.target.value)} /></Field>
  </CommandForm>
}
