import { useEffect, useRef, useState, type ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router'
import { ErrorBanner, Field, Loading, Notice } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import type { components } from '@/gen/api'
import { typedRequest } from '@/lib/api'
import { addDays, fmtDate, today } from '@/lib/format'
import { t, tv , runtimeText } from '@/lib/i18n'
import { CoordinationRegister, type DetailContext, type RegisterContext } from './CoordinationRegister'
import { CommandForm, CoordStatus, FieldsCommand, PersonLabel, SelectField, SourceLink, discName, peopleChoices, disciplineChoices, type FormField } from './CoordinationForms'
import { useSafeForm } from '@/hooks/drafts'
import { RecordContext, RecordContextActors } from '@/components/hub/record-context'
import { useItemPanel } from '@/components/hub/panel-host'
import { useFlag, useMe } from '@/lib/auth'
import { captureRecordContextOrigin } from '@/lib/record-context-navigation'
import { useWorkRecordOrigin } from './work-record-navigation'
import { ReviewOpenFindings } from './review-open-findings'
import { ReviewResponseForm, reviewResponseOutcomeLabel } from './review-response-form'
import { ExportMenu } from '@/components/hub/export'
import { ReviewDisciplineSummary } from './review-discipline-summary'
import { ReviewDisciplineDefaultSuggestions, useReviewDisciplineDefaults } from './review-discipline-defaults'
import { ExternalFindingContext, ExternalReviewForm, IssuedReviewPackageForm, IssuedSourceLabel } from './external-review-form'
import { FormNavigationScope } from '@/components/hub/form-navigation-context'
import { leave } from '@/lib/drafts'

type Schemas = components['schemas']
type Assignment = Schemas['ReviewAssignmentRow']
type Finding = Schemas['ReviewFindingRow']
type Detail = Schemas['ReviewDetail']
const reason: FormField = { name: 'reason', label: 'coord.reason', type: 'textarea' }
const fieldset = 'space-y-1 rounded-lg border p-4', legend = 'px-1 text-sm font-semibold', check = 'mt-0.5 size-4 shrink-0 accent-(--primary)'
const subhead = 'text-base/6 font-semibold'
export function ReviewsTab() { return <CoordinationRegister kind="reviews" statuses={['Draft', 'In Review', 'Changes Required', 'Approved', 'Superseded', 'Cancelled']} create={c => <ReviewForm {...c} />} detail={c => <ReviewDetail key={c.findingId ?? c.id} {...c} />} extra={c => <div className="space-y-4"><ReviewDisciplineSummary projectId={c.projectId} /><ReviewOpenFindings projectId={c.projectId} projectNumber={c.number} /></div>} /> }
function ReviewForm(props: RegisterContext & { close: () => void; existing?: Detail; available?: boolean; onCheck?: () => void; onReturnFocus?: () => void }) {
  const registerOn = useFlag('register_screens'), [issued, setIssued] = useState(false)
  const external = registerOn && (props.existing ? props.existing.manifest.some(row => !!row.issuedSource) : issued)
  const sourceModeControl = registerOn && !props.existing ? <SelectField label={t('externalReview.sourceMode')} value={issued ? 'issued' : 'internal'} onChange={value => leave(() => setIssued(value === 'issued'))} choices={[{ value: 'internal', label: t('externalReview.internalMode') }, { value: 'issued', label: t('externalReview.issuedMode') }]} /> : undefined
  return external ? <IssuedReviewPackageForm {...props} sourceModeControl={sourceModeControl} /> : <InternalReviewForm {...props} sourceModeControl={sourceModeControl} />
}
function InternalReviewForm({ options: o, projectId, refresh, open, close, existing, sourceModeControl }: RegisterContext & { close: () => void; existing?: Detail; sourceModeControl?: ReactNode }) {
  const noticesOn = useFlag('capacity_screens')
  const patternsOn = useFlag('screen_patterns')
  const registerOn = useFlag('register_screens'), assignmentsManual = useRef(false)
  const p = existing?.package
  const [title, setTitle] = useState(p?.title ?? ''), [purpose, setPurpose] = useState(p?.purpose ?? ''), [coordinator, setCoordinator] = useState(p?.coordinatorId ?? ''), [discipline, setDiscipline] = useState(p?.projectDisciplineId ?? o.manageDisciplineIds[0] ?? '')
  const [required, setRequired] = useState(p?.requiredForIssue ?? true), [why, setWhy] = useState(''), [impact, setImpact] = useState('')
  const [sources, setSources] = useState<string[]>([])
  const [assignments, setAssignments] = useState((existing?.assignments.filter(a => a.roundId === p?.currentRoundId) ?? []).map(a => ({ projectDisciplineId: a.projectDisciplineId, reviewerId: a.reviewerId, dueDate: addDays(today(), 7) })))
  const safe = useSafeForm(p ? null : `review.new.${projectId}`, { title, purpose, coordinator, discipline, required, why, impact, sources, assignments }, (d) => { assignmentsManual.current = true; setTitle(d.title); setPurpose(d.purpose); setCoordinator(d.coordinator); setDiscipline(d.discipline); setRequired(d.required); setWhy(d.why); setImpact(d.impact); setSources(d.sources); setAssignments(d.assignments) }) // FR-SAV-03
  const defaults = useReviewDisciplineDefaults({ projectId, sources, enabled: registerOn && !p, manual: assignmentsManual, assignments, setAssignments })
  const editAssignments: typeof setAssignments = next => { assignmentsManual.current = true; setAssignments(next) }
  const sourceRows = o.sources.filter(s => s.published && s.isCurrent && s.revision.deliverableId)
  const sourceIds = [...sources].sort().join(',')
  const guidance = useQuery({ queryKey: ['review-assignment-guidance', projectId, sourceIds], enabled: patternsOn && sources.length > 0,
    queryFn: () => typedRequest('get', '/api/v1/projects/{projectId}/review-assignment-guidance', { params: { projectId }, query: { sourceRevisionIds: sourceIds } }) })
  const rules = guidance.data
  useEffect(() => {
    if (!patternsOn || !rules) return
    setAssignments(rows => rows.some(a => rules.excludedReviewerIds.includes(a.reviewerId))
      ? rows.map(a => rules.excludedReviewerIds.includes(a.reviewerId) ? { ...a, reviewerId: '' } : a) : rows)
  }, [patternsOn, rules])
  const reviewers = peopleChoices(o, noticesOn).filter(person => !patternsOn || (!!rules && !rules.excludedReviewerIds.includes(person.value)))
    .map(person => patternsOn && rules?.allowSelfReview && rules.authorIds.includes(person.value) ? { ...person, label: `${person.label} · ${t('workReview.selfAllowed')}` } : person)
  const unresolved = defaults.unresolved || (patternsOn && (!rules || !rules.sourcesAvailable || guidance.isPending || !!guidance.error || assignments.length === 0 || assignments.some(a => !a.reviewerId || rules.excludedReviewerIds.includes(a.reviewerId))))
  const assign = (id: string, field: string, value: string) => editAssignments(v => v.map(a => a.projectDisciplineId === id ? { ...a, [field]: value } : a))
  return <CommandForm title={t(p ? 'review.newRound' : 'reviews.new')} hint={t('review.formHint')} safe={safe} submitDisabled={unresolved} path={`projects/${projectId}/reviews${p ? `/${p.id}/rounds` : ''}`} payload={() => p ? { rowVersion: p.rowVersion, purpose, sourceRevisionIds: sources, assignments, reason: why, removalImpact: impact || null } : { title, purpose, projectDisciplineId: discipline, coordinatorId: coordinator, sourceRevisionIds: sources, assignments, requiredForIssue: required, reason: why || null }} onClose={close} onDone={id => { refresh(); close(); open(p?.id ?? id) }}>
    {sourceModeControl}
    {!p && <><Field label={t('coord.title')} htmlFor="review-title"><Input id="review-title" required value={title} onChange={e => setTitle(e.target.value)} /></Field>
      <div className="grid gap-3 sm:grid-cols-2"><SelectField label={t('coord.discipline')} value={discipline} onChange={setDiscipline} choices={disciplineChoices(o).filter(d => o.manageDisciplineIds.includes(d.value))} /><SelectField label={t('coord.coordinator')} value={coordinator} onChange={setCoordinator} choices={peopleChoices(o, noticesOn)} /></div></>}
    <Field label={t('review.purpose')} htmlFor="review-purpose"><Textarea id="review-purpose" required maxLength={2000} value={purpose} onChange={e => setPurpose(e.target.value)} /></Field>
    <fieldset className={fieldset}><legend className={legend}>{t('review.manifest')}</legend><p className="pb-2 text-xs/[18px] text-muted-foreground">{t('review.manifestHint')}</p>
      {!sourceRows.length && <Notice title={t('review.noSources')} />}
      {sourceRows.map(s => <label key={s.revision.id} className="flex items-start gap-2 py-1.5 text-sm"><input type="checkbox" className={check} checked={sources.includes(s.revision.id)} onChange={e => setSources(v => e.target.checked ? [...v, s.revision.id] : v.filter(id => id !== s.revision.id))} />{s.revision.sourceKey} · {s.revision.revision} · {s.revision.title}</label>)}</fieldset>
    <fieldset className={fieldset}><legend className={legend}>{t('review.requiredDisciplines')}</legend>
      <ReviewDisciplineDefaultSuggestions state={defaults} />
      {patternsOn && <p className="text-xs text-muted-foreground">{rules ? rules.allowSelfReview ? t('workReview.selfAllowed') : t('workReview.packageIndependentHint') : t('workReview.startUnavailable')}</p>}
      {guidance.error && <ErrorBanner error={guidance.error} retry={() => guidance.refetch()} />}
      {o.disciplines.map(d => { const a = assignments.find(a => a.projectDisciplineId === d.id); return <div key={d.id} className="border-b py-3 last:border-b-0"><label className="flex items-center gap-2 text-sm font-medium"><input type="checkbox" className={check} checked={!!a} onChange={e => editAssignments(v => e.target.checked ? [...v, { projectDisciplineId: d.id, reviewerId: '', dueDate: addDays(today(), 7) }] : v.filter(a => a.projectDisciplineId !== d.id))} />{d.name}</label>{a && <div className="mt-3 grid gap-3 sm:grid-cols-2"><div><SelectField label={t('review.reviewer')} value={a.reviewerId} onChange={v => assign(d.id, 'reviewerId', v)} choices={reviewers} />{patternsOn && !a.reviewerId && <p className="mt-1 text-xs text-warn">▲ {t('workReview.missing')}</p>}</div><Field label={t('review.due')} htmlFor={`review-due-${d.id}`}><Input id={`review-due-${d.id}`} type="date" required value={a.dueDate} onChange={e => assign(d.id, 'dueDate', e.target.value)} /></Field></div>}</div> })}</fieldset>
    {!p && <label className="flex items-start gap-2 text-sm"><input type="checkbox" className={check} checked={required} onChange={e => setRequired(e.target.checked)} />{t('review.requireForIssue')}</label>}
    <Field label={t('coord.reason')} htmlFor="review-reason" optional={!p}><Textarea id="review-reason" required={!!p} value={why} onChange={e => setWhy(e.target.value)} /></Field>
    {p && <Field label={t('review.removalImpact')} htmlFor="review-impact" hint={t('review.removalHint')}><Textarea id="review-impact" value={impact} onChange={e => setImpact(e.target.value)} /></Field>}
  </CommandForm>
}
function ReviewDetail(c: DetailContext) {
  const followRelatedLink = useWorkRecordOrigin()
  const { options: o, projectId, number, id, close } = c
  const me = useMe(), openPanel = useItemPanel()
  const recordTrigger = useRef<HTMLButtonElement>(null), newRoundTrigger = useRef<HTMLButtonElement>(null), packageTitle = useRef<HTMLHeadingElement>(null)
  const focusAvailability = useRef({ external: false, round: false })
  const patternsOn = useFlag('screen_patterns'), peopleOn = useFlag('people_screens'), registerOn = useFlag('register_screens'), navigate = useNavigate(), location = useLocation(), [searchParams, setSearchParams] = useSearchParams()
  const q = useQuery({ queryKey: ['review-detail', projectId, id, me.id, registerOn, patternsOn], queryFn: () => typedRequest('get', '/api/v1/projects/{projectId}/reviews/{id}', { params: { projectId, id } }) })
  const issues = useQuery({ queryKey: ['review-issues', projectId], queryFn: () => typedRequest('get', '/api/v1/projects/{id}/issues', { params: { id: projectId } }), enabled: !!q.data?.findings.some(f => !!f.issueId), staleTime: 30_000 })
  const [roundId, setRoundId] = useState(c.findingRoundId ?? (patternsOn ? searchParams.get('round') : null) ?? ''), [action, setAction] = useState<{ type: string; assignment?: Assignment; finding?: Finding } | null>(null)
  const selectRound = (value: string) => {
    setRoundId(value)
    if (!patternsOn) return
    const next = new URLSearchParams(searchParams)
    if (value) next.set('round', value); else next.delete('round')
    setSearchParams(next)
  }
  const findingTo = (findingId: string, findingRoundId: string) => {
    const next = new URLSearchParams(location.search)
    next.set('panel', `ReviewFinding:${findingId}`)
    next.set('round', findingRoundId)
    return { pathname: location.pathname, search: `?${next.toString()}` }
  }
  const d = q.data, p = d?.package
  const requestedRound = c.findingId && c.findingRoundId ? d?.rounds.find(r => r.id === c.findingRoundId) : null
  const selectedRoundId = patternsOn ? searchParams.get('round') : roundId
  const round = c.findingId ? requestedRound : d?.rounds.find(r => r.id === (selectedRoundId || p?.currentRoundId))
  const exactFinding = c.findingId && c.findingRoundId ? d?.findings.find(f => f.id === c.findingId && f.roundId === c.findingRoundId) : null
  const exactMissing = Boolean(c.findingId && (!c.findingRoundId || !requestedRound || !exactFinding))
  const current = round?.id === p?.currentRoundId
  const live = d?.canWrite && current && p && !['Superseded', 'Cancelled'].includes(p.status)
  const presentation = patternsOn ? d?.presentation?.find(row => row.roundId === round?.id) : undefined
  const issuedMode = registerOn && !!d?.manifest.some(row => !!row.issuedSource)
  const roundSources = d?.manifest.filter(row => row.roundId === round?.id) ?? []
  const issuedIds = roundSources.flatMap(row => row.issuedSource ? [row.issuedSource.deliverableIssueId] : []).sort().join(',')
  const issuedGuidance = useQuery({ queryKey: ['review-issued-assignment-guidance', projectId, issuedIds], enabled: issuedMode && !!issuedIds,
    queryFn: () => typedRequest('get', '/api/v1/projects/{projectId}/review-issued-assignment-guidance', { params: { projectId }, query: { issuedRevisionIds: issuedIds } }) })
  const issuedRules = issuedGuidance.isSuccess && !issuedGuidance.isError ? issuedGuidance.data : undefined
  const canStartIssued = !!issuedRules?.sourcesAvailable && !issuedGuidance.isFetching && !!d?.assignments.some(row => row.roundId === round?.id) && !d?.assignments.some(row => row.roundId === round?.id && (!row.reviewerId || issuedRules.excludedReviewerIds.includes(row.reviewerId)))
  const recordAvailable = !!(issuedMode && current && round?.startedAt && d?.canRecordExternalReview && q.isSuccess && !q.isError)
  const roundAvailable = !!(issuedMode && q.isSuccess && !q.isError && !q.isFetching && live && d?.canCoordinate)
  focusAvailability.current = { external: recordAvailable, round: roundAvailable }
  const returnReviewFocus = (kind: 'external' | 'round') => {
    const trigger = kind === 'external' ? recordTrigger.current : newRoundTrigger.current
    const target = focusAvailability.current[kind] && trigger?.isConnected && !trigger.disabled ? trigger : packageTitle.current
    if (target?.isConnected && target.getClientRects().length) target.focus()
  }
  const done = () => { setAction(null); c.refresh(); q.refetch() }
  if (action?.type === 'round' && d && !issuedMode) return <ReviewForm {...c} existing={d} close={() => setAction(null)} />
  return <><Dialog open={action?.type !== 'round' || !roundAvailable} onOpenChange={v => !v && close()}><DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-4xl"><DialogHeader><DialogTitle ref={packageTitle} tabIndex={issuedMode ? -1 : undefined}>{p ? `${p.key} · ${p.title}` : t('reviews.title')}</DialogTitle><DialogDescription>{t('review.detailHint')}</DialogDescription></DialogHeader>
    {q.isPending ? <Loading rows={4} /> : q.error ? <ErrorBanner error={q.error} retry={() => q.refetch()} /> : d && p && (exactMissing || (patternsOn && !round)) ? <ErrorBanner error={new Error(t('common.unavailable'))} retry={() => q.refetch()} /> : d && p && round && <div className="space-y-6 text-sm">
      {c.findingContext && <><p className="rounded-md border bg-muted/30 px-3 py-2 text-sm font-medium"><span className="key">{c.findingKey}</span>{c.findingTitle ? ` · ${c.findingTitle}` : ''}</p><RecordContext context={c.findingContext} projectNumber={number} meId={me.id} openPanel={openPanel} /></>}
      <div className="flex flex-wrap items-center gap-2">
        <CoordStatus status={p.status} />
        {p.requiredForIssue && <span className="rounded-md bg-secondary px-2 py-0.5 text-xs/[18px] font-medium">{t('review.issueGate')}</span>}
        <span className="flex-1" />
        {registerOn && <ExportMenu path={`projects/${projectId}/reviews/${p.id}/response-sheet`} params={{}} name={`${number}-${p.key}-responses`} label={t('reviewResponse.export')} />}
        {recordAvailable && <Button ref={recordTrigger} size="sm" onClick={() => setAction({ type: 'external' })}>{t('externalReview.record')}</Button>}
        {live && d.canCoordinate && <>{p.status === 'Draft' && <Button size="sm" disabled={issuedMode ? !canStartIssued || (patternsOn && !presentation?.startAllowed) : patternsOn && !presentation?.startAllowed} title={patternsOn ? presentation?.startReason ?? (!presentation ? t('workReview.startUnavailable') : undefined) : undefined} onClick={() => setAction({ type: 'start' })}>{t('review.start')}</Button>}<Button ref={newRoundTrigger} size="sm" variant="outline" onClick={() => setAction({ type: 'round' })}>{t('review.newRound')}</Button></>}
        {live && d.canManage && <><Button size="sm" variant="outline" onClick={() => setAction({ type: 'coordinator' })}>{t('review.assignCoordinator')}</Button><Button size="sm" variant="ghost" className="text-bad hover:text-bad" onClick={() => setAction({ type: 'cancel' })}>{t('review.cancel')}</Button></>}
      </div>
      {registerOn && <p className="text-xs text-muted-foreground">{t('reviewResponse.exportHint')}</p>}
      {patternsOn && p.status === 'Draft' && !presentation?.startAllowed && <p role="status" className="text-xs text-warn">▲ {presentation?.startReason ?? t('workReview.startUnavailable')}</p>}
      <dl className="grid grid-cols-[minmax(8rem,auto)_1fr] gap-x-4 gap-y-2">
        <dt className="text-muted-foreground">{t('coord.coordinator')}</dt><dd><PersonLabel options={o} id={p.coordinatorId} focusKey={`review:${p.id}:coordinator`} /></dd>
      </dl>
      <p className="whitespace-pre-wrap text-base/6">{p.purpose}</p>
      {!c.findingId && <div className="max-w-sm"><SelectField label={t('review.roundHistory')} value={round.id} onChange={selectRound} choices={d.rounds.map(r => ({ value: r.id, label: `${t('review.round')} ${r.number} · ${tv(r.status)}` }))} /></div>}
      {!current && <Notice title={t('review.frozenRound')} />}{round.reason && <p className="whitespace-pre-wrap">{round.reason}</p>}{round.removalImpact && <p>{t('review.removalImpact')}: {round.removalImpact}</p>}
      <section className="space-y-2"><h3 className={subhead}>{t('review.manifest')}</h3><ul className="space-y-2">{roundSources.map(m => <li key={m.id}>{registerOn && m.issuedSource ? <IssuedSourceLabel source={m.issuedSource} /> : m.source ? <SourceLink source={m.source} /> : t('common.unavailable')}</li>)}</ul><p className="text-xs/[18px] text-muted-foreground">{t(issuedMode ? 'externalReview.supplementary' : 'handoff.manualRevision')}</p>
        {issuedMode && <><h4 className="text-sm font-medium">{t('externalReview.authorsCaptured')}</h4><ul className="space-y-1">{roundSources.map(source => <li key={source.id}>{source.issuedSource && <IssuedSourceLabel source={source.issuedSource} />}: {source.issuedSource?.authorsState === 'Captured' && source.authorIds.length ? source.authorIds.map((author, index) => <span key={author}>{index > 0 && ', '}<PersonLabel options={o} id={author} focusKey={`review:${p.id}:manifest:${source.id}:author:${author}`} /></span>) : t('externalReview.authorsUnknown')}</li>)}</ul>{issuedGuidance.error && <ErrorBanner error={issuedGuidance.error} retry={() => issuedGuidance.refetch()} />}</>}
      </section>
      <section className="space-y-2"><h3 className={subhead}>{t('review.requiredDisciplines')}</h3><div className="grid gap-3 sm:grid-cols-2">{d.assignments.filter(a => a.roundId === round.id).map(a => <div key={a.id} className="space-y-2 rounded-lg border p-4">
        <div className="flex flex-wrap items-center justify-between gap-2"><h4 className="font-semibold">{discName(o, a.projectDisciplineId)}</h4><CoordStatus status={a.status} /></div>
        {patternsOn ? <dl className="space-y-2">
          {!issuedMode && <div><dt className="text-xs text-muted-foreground">{t('workReview.sourcePerformers')}</dt><dd>{presentation?.guidance?.sources.length ? <ul className="space-y-1">{presentation.guidance.sources.map(source => <li key={source.sourceRevisionId}><span className="key">{source.sourceKey}</span>: {source.available && source.performers.length ? source.performers.map((performer, index) => <span key={performer.taskKey}>{index > 0 && '; '}{performer.taskKey} · <RecordContextActors actors={[performer.person]} actorState={performer.person.state} meId={d.actorId} focusKey={`review:${p.id}:round:${round.id}:assignment:${a.id}:source:${source.sourceRevisionId}:performer:${performer.taskKey}`} /></span>) : t('workReview.performersUnknown')}</li>)}</ul> : t('workReview.performersUnknown')}</dd></div>}
          <div><dt className="text-xs text-muted-foreground">{t((issuedMode ? issuedRules : presentation?.guidance)?.allowSelfReview ? 'field.ReviewerId' : 'workReview.reviewer')}</dt><dd>{(() => { const reviewer = presentation?.reviewers.find(row => row.assignmentId === a.id)?.reviewer; return reviewer ? <RecordContextActors actors={[reviewer]} actorState={reviewer.state} meId={d.actorId} focusKey={`review:${p.id}:round:${round.id}:assignment:${a.id}:reviewer`} /> : <PersonLabel options={o} id={a.reviewerId} focusKey={`review:${p.id}:round:${round.id}:assignment:${a.id}:reviewer:fallback`} /> })()}</dd></div>
          {presentation?.missingReviewerAssignmentIds.includes(a.id) && <div><dt className="sr-only">{t('review.reviewer')}</dt><dd className="text-warn">▲ {t('workReview.missing')}</dd></div>}
          {(issuedMode ? issuedRules : presentation?.guidance)?.allowSelfReview && (issuedMode ? issuedRules : presentation?.guidance)?.authorIds.includes(a.reviewerId) && <div><dt className="sr-only">{t('review.reviewer')}</dt><dd className="text-xs text-muted-foreground">{t('workReview.selfAllowed')}</dd></div>}
        </dl> : <p className="flex flex-wrap items-center gap-x-2 gap-y-1"><PersonLabel options={o} id={a.reviewerId} focusKey={`review:${p.id}:round:${round.id}:assignment:${a.id}:reviewer:legacy`} /><span className="text-muted-foreground tabular-nums">· {fmtDate(a.dueDate)}</span></p>}
        {patternsOn && <p className="text-xs text-muted-foreground tabular-nums">{t('review.due')}: {fmtDate(a.dueDate)}</p>}
        {a.rationale && <p className="whitespace-pre-wrap">{a.rationale}</p>}
        <div className="flex flex-wrap gap-2">{live && round.startedAt && a.reviewerId === d.actorId && <Button size="sm" onClick={() => setAction({ type: 'decision', assignment: a })}>{t('review.recordDecision')}</Button>}{live && d.canCoordinate && <Button size="sm" variant="outline" onClick={() => setAction({ type: 'reviewer', assignment: a })}>{t('review.assignReviewer')}</Button>}</div>
      </div>)}</div></section>
      <section className="space-y-3"><div className="flex items-center justify-between gap-3"><h3 className={subhead}>{t('review.findings')}</h3>{!issuedMode && live && round.startedAt && d.assignments.some(a => a.roundId === round.id && a.reviewerId === d.actorId) && <Button size="sm" onClick={() => setAction({ type: 'finding' })}>{t('review.addFinding')}</Button>}</div>
        {!d.findings.some(f => f.roundId === round.id && (!c.findingId || f.id === c.findingId)) && <p className="text-muted-foreground">{t('review.noFindings')}</p>}
        {d.findings.filter(f => f.roundId === round.id && (!c.findingId || f.id === c.findingId)).map(f => <article key={f.id} className="space-y-2 rounded-lg border p-4">
          <div className="flex flex-wrap items-center gap-2"><CoordStatus status={f.severity} /><CoordStatus status={f.status} /></div>
          {!c.findingId && f.context && <><RecordContext context={f.context} projectNumber={number} meId={me.id} openPanel={openPanel} />{patternsOn && <Link className="text-primary underline underline-offset-4" to={findingTo(f.id, f.roundId)} onClick={e => { if (e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return; e.preventDefault(); navigate(findingTo(f.id, f.roundId), { state: { recordContextReturn: captureRecordContextOrigin(location, e.currentTarget) } }) }}>{t('recordContext.openFinding')}</Link>}</>}
          <p className="whitespace-pre-wrap">{f.text}</p>
          {registerOn && f.externalReviewId && <ExternalFindingContext detail={d} finding={f} options={o} />}
          {f.issueId && <p>{t('review.linkedIssue')}: {issues.data?.find(i => i.id === f.issueId) ? <Link className="text-primary underline underline-offset-4" to={`/projects/${number}/issues?panel=Issue:${f.issueId}`} onClick={followRelatedLink}><span className="key">{issues.data.find(i => i.id === f.issueId)!.key}</span></Link> : <span className="text-muted-foreground">{issues.isPending ? t('review.issueLoading') : t('review.issueUnavailable')}</span>}</p>}
          {f.carriedFromId && <p className="text-xs/[18px] text-muted-foreground">{t('review.carried')}</p>}
          <dl className="grid grid-cols-[minmax(8rem,auto)_1fr] gap-x-4 gap-y-2">
            <dt className="text-muted-foreground">{t('review.resolver')}</dt><dd><PersonLabel options={o} id={f.resolverId} focusKey={`review:${p.id}:finding:${f.id}:resolver`} /></dd>
            <dt className="text-muted-foreground">{t('review.verifier')}</dt><dd>{registerOn && f.externalReviewId && !f.verifierId ? <span className="text-muted-foreground">{t('externalReview.unassigned')}</span> : <PersonLabel options={o} id={f.verifierId} focusKey={`review:${p.id}:finding:${f.id}:verifier`} />}</dd>
            {registerOn && <><dt className="text-muted-foreground">{t('reviewResponse.outcome')}</dt><dd>{reviewResponseOutcomeLabel(f.responseOutcome)}</dd></>}
          </dl>
          {f.response && <p className="whitespace-pre-wrap">{f.response}</p>}{f.evidenceUrl && <a className="text-primary underline underline-offset-4" href={f.evidenceUrl} target="_blank" rel="noopener noreferrer">{t('coord.evidence')}</a>}{f.withdrawalAcknowledgedBy && <p>{t('review.withdrawalAcknowledged')}</p>}
          {live && <div className="flex flex-wrap gap-2">{f.resolverId === d.actorId && f.status === 'Open' && <Button size="sm" onClick={() => setAction({ type: 'Responded', finding: f })}>{t('review.respond')}</Button>}{f.verifierId === d.actorId && <>{f.status === 'Responded' && <><Button size="sm" onClick={() => setAction({ type: 'Verified Closed', finding: f })}>{t('review.verify')}</Button><Button size="sm" variant="outline" onClick={() => setAction({ type: 'Open', finding: f })}>{t('review.return')}</Button></>}{['Open', 'Responded'].includes(f.status) && <Button size="sm" variant="outline" onClick={() => setAction({ type: 'Withdrawn', finding: f })}>{t('review.withdraw')}</Button>}</>}{f.status === 'Withdrawn' && f.severity === 'Blocking' && !f.withdrawalAcknowledgedBy && p.coordinatorId === d.actorId && <Button size="sm" onClick={() => setAction({ type: 'acknowledgeWithdrawal', finding: f })}>{t('review.ackWithdrawal')}</Button>}{['Open', 'Responded'].includes(f.status) && <>{d.canCoordinate && <Button size="sm" variant="outline" onClick={() => setAction({ type: 'assignResolver', finding: f })}>{t('review.assignResolver')}</Button>}{d.canReassignVerifier && <Button size="sm" variant="outline" onClick={() => setAction({ type: 'assignVerifier', finding: f })}>{t('review.assignVerifier')}</Button>}</>}</div>}
          <details id={`review-finding-history-${p.id}-${f.id}`} data-person-source-disclosure={peopleOn ? '' : undefined} className="border-t pt-2"><summary className="cursor-pointer py-1 font-medium">{t('coord.history')}</summary><ol className="mt-2 space-y-2">{d.events.filter(e => e.findingId === f.id).map(e => <li key={e.id}><span className="font-medium">{tv(e.action)}</span> · <PersonLabel options={o} id={e.createdBy} focusKey={`review:${p.id}:finding:${f.id}:event:${e.id}:actor`} /> · <span className="tabular-nums">{new Date(e.createdAt).toLocaleString()}</span>{registerOn && e.action === 'Responded' && <p>{t('reviewResponse.outcome')}: {reviewResponseOutcomeLabel(e.responseOutcome)}</p>}<p className="whitespace-pre-wrap">{e.reason}</p>{registerOn && f.externalReviewId && e.action === 'Verified Closed' && e.evidenceUrl && <a className="text-primary underline underline-offset-4" href={e.evidenceUrl} target="_blank" rel="noopener noreferrer">{t('externalReview.verificationEvidence')}</a>}</li>)}</ol></details>
        </article>)}
      </section>
    </div>}
    {issuedMode && action && (q.isError || action.type === 'external' && !recordAvailable || action.type === 'round' && !roundAvailable) && <div className="space-y-2"><p role="status" className="text-sm text-muted-foreground">{t('externalReview.unavailable')}</p><Button size="sm" variant="outline" disabled={q.isFetching} onClick={() => q.refetch()}>{t('externalReview.check')}</Button></div>}
  </DialogContent></Dialog>{action && d && p && (action.type === 'round' ? <FormNavigationScope available={roundAvailable}><ReviewForm {...c} existing={d} available={roundAvailable} onCheck={() => q.refetch()} onReturnFocus={() => returnReviewFocus('round')} close={() => setAction(null)} /></FormNavigationScope> : action.type === 'external' ? <ExternalReviewForm projectId={projectId} detail={d} options={o} available={recordAvailable} onClose={() => setAction(null)} onDone={done} onCheck={() => q.refetch()} onReturnFocus={() => returnReviewFocus('external')} /> : <FormNavigationScope available={issuedMode ? q.isSuccess && !q.isError : undefined}><ReviewCommand c={c} d={d} action={action} close={() => setAction(null)} done={done} /></FormNavigationScope>)}</>
}
function ReviewCommand({ c, d, action, close, done }: { c: RegisterContext & { id: string }; d: Detail; action: { type: string; assignment?: Assignment; finding?: Finding }; close: () => void; done: () => void }) {
  const noticesOn = useFlag('capacity_screens')
  const patternsOn = useFlag('screen_patterns')
  const registerOn = useFlag('register_screens')
  const { type, assignment: a, finding: f } = action, p = d.package, o = c.options, base = `projects/${c.projectId}/reviews/${p.id}`
  const issues = useQuery({ queryKey: ['review-issues', c.projectId], queryFn: () => typedRequest('get', '/api/v1/projects/{id}/issues', { params: { id: c.projectId } }), enabled: type === 'finding', staleTime: 30_000 })
  let path = `${base}/action`, title = runtimeText(`review.command.${type}`), fields: FormField[] = [reason], initial: Record<string, string> = {}, build: (v: Record<string, string>) => object = v => ({ rowVersion: p.rowVersion, action: type, reason: v.reason })
  if (type === 'start') fields = []
  const issuedMode = registerOn && d.manifest.some(row => !!row.issuedSource), roundId = a?.roundId ?? p.currentRoundId
  const issuedIds = d.manifest.filter(row => row.roundId === roundId).flatMap(row => row.issuedSource ? [row.issuedSource.deliverableIssueId] : []).sort().join(',')
  const issuedGuidance = useQuery({ queryKey: ['review-issued-assignment-guidance', c.projectId, issuedIds], enabled: issuedMode && !!issuedIds && type === 'reviewer', queryFn: () => typedRequest('get', '/api/v1/projects/{projectId}/review-issued-assignment-guidance', { params: { projectId: c.projectId }, query: { issuedRevisionIds: issuedIds } }) })
  const guidance = issuedMode ? issuedGuidance.isSuccess && !issuedGuidance.isError ? issuedGuidance.data : undefined : d.presentation?.find(row => row.roundId === roundId)?.guidance
  if (type === 'coordinator' || type === 'reviewer') { path = type === 'reviewer' ? `${base}/assignments/${a!.id}/assign` : `${base}/assign`; fields = [{ name: 'ownerId', label: type === 'reviewer' ? 'review.reviewer' : 'coord.coordinator', choices: peopleChoices(o, noticesOn).filter(person => type !== 'reviewer' || (!patternsOn && !issuedMode) || (!!guidance && (!issuedMode || guidance.sourcesAvailable) && !guidance.excludedReviewerIds.includes(person.value))) }, reason]; build = v => ({ ...v, rowVersion: a?.rowVersion ?? p.rowVersion }) }
  if (type === 'decision') { path = `${base}/assignments/${a!.id}/decision`; fields = [{ name: 'status', label: 'common.status', choices: ['In Review', 'Changes Required', 'Approved'].map(s => ({ value: s, label: tv(s) })) }, { name: 'rationale', label: 'coord.rationale', type: 'textarea' }]; initial = { status: a!.status === 'Pending' ? 'In Review' : a!.status }; build = v => ({ ...v, rowVersion: a!.rowVersion }) }
  if (type === 'finding') { path = `${base}/findings`; fields = [{ name: 'sourceRevisionId', label: 'review.source', choices: d.manifest.filter(m => m.roundId === p.currentRoundId).flatMap(m => m.sourceRevisionId && m.source ? [{ value: m.sourceRevisionId, label: `${m.source.sourceKey} · ${m.source.revision}` }] : []) }, { name: 'projectDisciplineId', label: 'coord.discipline', choices: disciplineChoices(o).filter(x => d.assignments.some(a => a.roundId === p.currentRoundId && a.reviewerId === d.actorId && a.projectDisciplineId === x.value)) }, { name: 'resolverId', label: 'review.resolver', choices: peopleChoices(o, noticesOn) }, { name: 'severity', label: 'review.severity', choices: ['Blocking', 'Advisory'].map(s => ({ value: s, label: tv(s) })) }, { name: 'issueId', label: 'review.linkedIssue', optional: true, choices: (issues.data ?? []).map(i => ({ value: i.id, label: `${i.key} · ${i.title} · ${tv(i.status)}` })) }, { name: 'text', label: 'review.findingText', type: 'textarea' }]; initial = { severity: 'Blocking' }; build = v => ({ ...v, rowVersion: p.rowVersion, ...(v.issueId ? { issueId: v.issueId } : {}) }) }
  if (f) { path = `${base}/findings/${f.id}/action`; if (type === 'Responded' || (registerOn && f.externalReviewId && type === 'Verified Closed')) fields.push({ name: 'evidenceUrl', label: type === 'Verified Closed' ? 'externalReview.verificationEvidence' : 'coord.evidenceUrl', type: 'url' }); if (type.startsWith('assign')) fields.unshift({ name: 'ownerId', label: type === 'assignVerifier' ? 'review.verifier' : 'review.resolver', choices: peopleChoices(o, noticesOn) }); build = v => ({ ...v, rowVersion: f.rowVersion, action: type }) }
  if (registerOn && type === 'Responded' && f) return <ReviewResponseForm projectId={c.projectId} packageId={p.id} finding={f} onClose={close} onDone={done} />
  return <FieldsCommand path={path} title={title} fields={fields} initial={initial} build={build} hint={registerOn && f?.externalReviewId && type === 'Verified Closed' ? t('externalReview.verificationHint') : registerOn && f?.externalReviewId && type === 'assignVerifier' ? t('externalReview.independence') : (patternsOn || issuedMode) && type === 'reviewer' && !guidance?.allowSelfReview ? t('workReview.packageIndependentHint') : undefined} onClose={close} onDone={done} />
}
