import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { ErrorBanner, Field, Loading } from '@/components/hub/common'
import { StatusPill } from '@/components/hub/pills'
import { ApiError, get } from '@/lib/api'
import { fmtDate, fmtTime, today } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { CommandForm, SelectField, personName, workChoices, workRef, type CoordOptions, type WorkRef } from './CoordinationForms'

type Assessment = { id: string; rowVersion: number; ownerId: string; state: string; intendedOutput: string; completionCriteria: string; evaluatedAt: string }
type Check = { id: string; rowVersion: number; code: string; applies: boolean | null; satisfied: boolean | null; reason?: string; evidenceUrl?: string; recordedBy?: string }
type Detail = { assessment: Assessment; checks: Check[]; unknown: string[]; blocked: string[] }
type Constraint = { id: string; rowVersion: number; category: string; description: string; removalOwnerId: string; affectedOwnerId: string;
  neededBy: string; sourceUrl: string; state: string; resolutionEvidenceUrl?: string; verifiedBy?: string; verifiedAt?: string }

export function ReadinessInspector({ projectId, options, close, done }: { projectId: string; options: CoordOptions; close: () => void; done: () => void }) {
  const [target, setTarget] = useState(''), [creating, setCreating] = useState(false), [editing, setEditing] = useState<Check | null>(null)
  const [raising, setRaising] = useState(false), [moving, setMoving] = useState<{ row: Constraint; state: string } | null>(null)
  const [type, id] = target.split(':'), work = workRef(options, type, id)
  const path = `projects/${projectId}/readiness/${type}/${id}`
  const q = useQuery({ queryKey: ['readiness-detail', projectId, type, id], enabled: !!work,
    queryFn: async () => { try { return await get<Detail>(path) } catch (e) { if (e instanceof ApiError && e.status === 404) return null; throw e } } })
  const constraints = useQuery({ queryKey: ['readiness-constraints', projectId, type, id], enabled: !!work,
    queryFn: () => get<Constraint[]>(`${path}/constraints`) })
  const changed = () => { setCreating(false); setEditing(null); setRaising(false); setMoving(null); q.refetch(); constraints.refetch(); done() }
  const canCreate = options.canWrite && work?.ownerId === options.actorId
  const canAssess = options.canWrite && work && options.manageDisciplineIds.includes(work.projectDisciplineId)
  if (creating && work) return <CreateAssessment path={path} rowVersion={work.rowVersion} close={() => setCreating(false)} done={changed} />
  if (editing && q.data) return <Applicability path={`${path}/checks/${encodeURIComponent(editing.code)}/applicability`} check={editing}
    assessmentVersion={q.data.assessment.rowVersion} close={() => setEditing(null)} done={changed} />
  if (raising && work) return <RaiseConstraint path={`${path}/constraints`} work={work} options={options} close={() => setRaising(false)} done={changed} />
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
    </div>)}
    {work && <section className="space-y-3 text-sm" aria-label={t('readiness.constraints')}>
      <h3 className="font-medium">{t('readiness.constraints')}</h3>
      {(canCreate || canAssess) && <Button size="sm" variant="outline" onClick={() => setRaising(true)}>{t('readiness.raiseConstraint')}</Button>}
      {constraints.isPending ? <Loading rows={3} /> : constraints.error ? <ErrorBanner error={constraints.error} retry={() => constraints.refetch()} /> : constraints.data?.length === 0 ? <p>{t('readiness.noWorkConstraints')}</p> :
        <ul className="space-y-2">{constraints.data?.map(c => <li key={c.id} className="space-y-2 rounded border p-3">
          <StatusPill status={c.state} /><p className="font-medium">{c.description}</p><p>{tv(c.category)} · {fmtDate(c.neededBy)}</p>
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
  </DialogContent></Dialog>
}

function RaiseConstraint({ path, work, options, close, done }: { path: string; work: WorkRef; options: CoordOptions; close: () => void; done: () => void }) {
  const [category, setCategory] = useState(''), [description, setDescription] = useState(''), [removalOwnerId, setOwner] = useState(''), [neededBy, setNeeded] = useState(today()), [sourceUrl, setSource] = useState('')
  return <CommandForm path={path} title={t('readiness.raiseConstraint')} hint={t('readiness.constraintHint')} onClose={close} onDone={done}
    payload={() => ({ targetRowVersion: work.rowVersion, category, description, removalOwnerId, neededBy, sourceUrl })}>
    <SelectField label={t('readiness.category')} value={category} onChange={setCategory} choices={['Handoff', 'Decision', 'Basis', 'Capacity', 'Review', 'Scope', 'Other'].map(v => ({ value: v, label: tv(v) }))} />
    <Field label={t('readiness.constraintDescription')} htmlFor="constraint-description"><Textarea id="constraint-description" required maxLength={2000} value={description} onChange={e => setDescription(e.target.value)} /></Field>
    <SelectField label={t('readiness.removalOwner')} value={removalOwnerId} onChange={setOwner} choices={options.people.filter(p => p.id !== work.ownerId).map(p => ({ value: p.id, label: p.displayName }))} />
    <Field label={t('readiness.neededBy')} htmlFor="constraint-needed"><Input id="constraint-needed" required type="date" value={neededBy} onChange={e => setNeeded(e.target.value)} /></Field>
    <Field label={t('readiness.constraintSource')} htmlFor="constraint-source"><Input id="constraint-source" required type="url" value={sourceUrl} onChange={e => setSource(e.target.value)} /></Field>
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
