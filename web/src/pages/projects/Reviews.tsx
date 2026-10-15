import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { ErrorBanner, Field, Loading, Notice } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { get } from '@/lib/api'
import { addDays, fmtDate, today } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { CoordinationRegister, type RegisterContext } from './CoordinationRegister'
import { CommandForm, CoordStatus, FieldsCommand, PersonLabel, SelectField, SourceLink, discName, personName, peopleChoices, disciplineChoices, type RecordVersion, type SourceRevision, type FormField } from './CoordinationForms'

type Package = RecordVersion & { title: string; key: string; purpose: string; projectDisciplineId: string; coordinatorId: string; status: string; currentRoundId: string; roundNumber: number; requiredForIssue: boolean }
type Assignment = RecordVersion & { roundId: string; projectDisciplineId: string; reviewerId: string; dueDate: string; status: string; rationale?: string; decidedAt?: string }
type Finding = RecordVersion & { roundId: string; sourceRevisionId: string; projectDisciplineId: string; resolverId: string; verifierId: string; text: string; severity: string; status: string; response?: string; evidenceUrl?: string; carriedFromId?: string; withdrawalAcknowledgedBy?: string; issueId?: string | null }
type ReviewIssue = { id: string; key: string; title: string; status: string; ownerName?: string }
type Detail = { package: Package; rounds: { id: string; number: number; status: string; purpose: string; reason?: string; removalImpact?: string; startedAt?: string }[]; assignments: Assignment[]; findings: Finding[]; manifest: { roundId: string; sourceRevisionId: string; source: SourceRevision }[]; events: { id: string; findingId: string; action: string; reason: string; createdAt: string; createdBy: string }[]; actorId: string; canWrite: boolean; canCoordinate: boolean; canManage: boolean; canReassignVerifier: boolean }
const reason: FormField = { name: 'reason', label: 'coord.reason', type: 'textarea' }
const fieldset = 'space-y-1 rounded-lg border p-4', legend = 'px-1 text-sm font-semibold', check = 'mt-0.5 size-4 shrink-0 accent-(--primary)'
const subhead = 'text-base/6 font-semibold'
export function ReviewsTab() { return <CoordinationRegister kind="reviews" statuses={['Draft', 'In Review', 'Changes Required', 'Approved', 'Superseded', 'Cancelled']} create={c => <ReviewForm {...c} />} detail={c => <ReviewDetail {...c} />} /> }
function ReviewForm({ options: o, projectId, refresh, open, close, existing }: RegisterContext & { close: () => void; existing?: Detail }) {
  const p = existing?.package
  const [title, setTitle] = useState(p?.title ?? ''), [purpose, setPurpose] = useState(p?.purpose ?? ''), [coordinator, setCoordinator] = useState(p?.coordinatorId ?? ''), [discipline, setDiscipline] = useState(p?.projectDisciplineId ?? o.manageDisciplineIds[0] ?? '')
  const [required, setRequired] = useState(p?.requiredForIssue ?? true), [why, setWhy] = useState(''), [impact, setImpact] = useState('')
  const [sources, setSources] = useState<string[]>([])
  const [assignments, setAssignments] = useState((existing?.assignments.filter(a => a.roundId === p?.currentRoundId) ?? []).map(a => ({ projectDisciplineId: a.projectDisciplineId, reviewerId: a.reviewerId, dueDate: addDays(today(), 7) })))
  const sourceRows = o.sources.filter(s => s.published && s.isCurrent && s.revision.deliverableId)
  const assign = (id: string, field: string, value: string) => setAssignments(v => v.map(a => a.projectDisciplineId === id ? { ...a, [field]: value } : a))
  return <CommandForm title={t(p ? 'review.newRound' : 'reviews.new')} hint={t('review.formHint')} path={`projects/${projectId}/reviews${p ? `/${p.id}/rounds` : ''}`} payload={() => p ? { rowVersion: p.rowVersion, purpose, sourceRevisionIds: sources, assignments, reason: why, removalImpact: impact || null } : { title, purpose, projectDisciplineId: discipline, coordinatorId: coordinator, sourceRevisionIds: sources, assignments, requiredForIssue: required, reason: why || null }} onClose={close} onDone={id => { refresh(); close(); open(p?.id ?? id) }}>
    {!p && <><Field label={t('coord.title')} htmlFor="review-title"><Input id="review-title" required value={title} onChange={e => setTitle(e.target.value)} /></Field>
      <div className="grid gap-3 sm:grid-cols-2"><SelectField label={t('coord.discipline')} value={discipline} onChange={setDiscipline} choices={disciplineChoices(o).filter(d => o.manageDisciplineIds.includes(d.value))} /><SelectField label={t('coord.coordinator')} value={coordinator} onChange={setCoordinator} choices={peopleChoices(o)} /></div></>}
    <Field label={t('review.purpose')} htmlFor="review-purpose"><Textarea id="review-purpose" required maxLength={2000} value={purpose} onChange={e => setPurpose(e.target.value)} /></Field>
    <fieldset className={fieldset}><legend className={legend}>{t('review.manifest')}</legend><p className="pb-2 text-xs/[18px] text-muted-foreground">{t('review.manifestHint')}</p>
      {!sourceRows.length && <Notice title={t('review.noSources')} />}
      {sourceRows.map(s => <label key={s.revision.id} className="flex items-start gap-2 py-1.5 text-sm"><input type="checkbox" className={check} checked={sources.includes(s.revision.id)} onChange={e => setSources(v => e.target.checked ? [...v, s.revision.id] : v.filter(id => id !== s.revision.id))} />{s.revision.sourceKey} · {s.revision.revision} · {s.revision.title}</label>)}</fieldset>
    <fieldset className={fieldset}><legend className={legend}>{t('review.requiredDisciplines')}</legend>{o.disciplines.map(d => { const a = assignments.find(a => a.projectDisciplineId === d.id); return <div key={d.id} className="border-b py-3 last:border-b-0"><label className="flex items-center gap-2 text-sm font-medium"><input type="checkbox" className={check} checked={!!a} onChange={e => setAssignments(v => e.target.checked ? [...v, { projectDisciplineId: d.id, reviewerId: '', dueDate: addDays(today(), 7) }] : v.filter(a => a.projectDisciplineId !== d.id))} />{d.name}</label>{a && <div className="mt-3 grid gap-3 sm:grid-cols-2"><SelectField label={t('review.reviewer')} value={a.reviewerId} onChange={v => assign(d.id, 'reviewerId', v)} choices={peopleChoices(o)} /><Field label={t('review.due')} htmlFor={`review-due-${d.id}`}><Input id={`review-due-${d.id}`} type="date" required value={a.dueDate} onChange={e => assign(d.id, 'dueDate', e.target.value)} /></Field></div>}</div> })}</fieldset>
    {!p && <label className="flex items-start gap-2 text-sm"><input type="checkbox" className={check} checked={required} onChange={e => setRequired(e.target.checked)} />{t('review.requireForIssue')}</label>}
    <Field label={t('coord.reason')} htmlFor="review-reason" optional={!p}><Textarea id="review-reason" required={!!p} value={why} onChange={e => setWhy(e.target.value)} /></Field>
    {p && <Field label={t('review.removalImpact')} htmlFor="review-impact" hint={t('review.removalHint')}><Textarea id="review-impact" value={impact} onChange={e => setImpact(e.target.value)} /></Field>}
  </CommandForm>
}
function ReviewDetail(c: RegisterContext & { id: string; close: () => void }) {
  const { options: o, projectId, number, id, close } = c
  const q = useQuery({ queryKey: ['review-detail', projectId, id], queryFn: () => get<Detail>(`projects/${projectId}/reviews/${id}`) })
  const issues = useQuery({ queryKey: ['review-issues', projectId], queryFn: () => get<ReviewIssue[]>(`projects/${projectId}/issues`), enabled: !!q.data?.findings.some(f => !!f.issueId), staleTime: 30_000 })
  const [roundId, setRoundId] = useState(''), [action, setAction] = useState<{ type: string; assignment?: Assignment; finding?: Finding } | null>(null)
  const d = q.data, p = d?.package, round = d?.rounds.find(r => r.id === (roundId || p?.currentRoundId)), current = round?.id === p?.currentRoundId
  const live = d?.canWrite && current && p && !['Superseded', 'Cancelled'].includes(p.status)
  const done = () => { setAction(null); c.refresh(); q.refetch() }
  if (action?.type === 'round' && d) return <ReviewForm {...c} existing={d} close={() => setAction(null)} />
  return <><Dialog open onOpenChange={v => !v && close()}><DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-4xl"><DialogHeader><DialogTitle>{p ? `${p.key} · ${p.title}` : t('reviews.title')}</DialogTitle><DialogDescription>{t('review.detailHint')}</DialogDescription></DialogHeader>
    {q.isPending ? <Loading rows={4} /> : q.error ? <ErrorBanner error={q.error} retry={() => q.refetch()} /> : d && p && round && <div className="space-y-6 text-sm">
      <div className="flex flex-wrap items-center gap-2">
        <CoordStatus status={p.status} />
        {p.requiredForIssue && <span className="rounded-md bg-secondary px-2 py-0.5 text-xs/[18px] font-medium">{t('review.issueGate')}</span>}
        <span className="flex-1" />
        {live && d.canCoordinate && <>{p.status === 'Draft' && <Button size="sm" onClick={() => setAction({ type: 'start' })}>{t('review.start')}</Button>}<Button size="sm" variant="outline" onClick={() => setAction({ type: 'round' })}>{t('review.newRound')}</Button></>}
        {live && d.canManage && <><Button size="sm" variant="outline" onClick={() => setAction({ type: 'coordinator' })}>{t('review.assignCoordinator')}</Button><Button size="sm" variant="ghost" className="text-bad hover:text-bad" onClick={() => setAction({ type: 'cancel' })}>{t('review.cancel')}</Button></>}
      </div>
      <dl className="grid grid-cols-[minmax(8rem,auto)_1fr] gap-x-4 gap-y-2">
        <dt className="text-muted-foreground">{t('coord.coordinator')}</dt><dd><PersonLabel options={o} id={p.coordinatorId} /></dd>
      </dl>
      <p className="whitespace-pre-wrap text-base/6">{p.purpose}</p>
      <div className="max-w-sm"><SelectField label={t('review.roundHistory')} value={round.id} onChange={setRoundId} choices={d.rounds.map(r => ({ value: r.id, label: `${t('review.round')} ${r.number} · ${tv(r.status)}` }))} /></div>
      {!current && <Notice title={t('review.frozenRound')} />}{round.reason && <p className="whitespace-pre-wrap">{round.reason}</p>}{round.removalImpact && <p>{t('review.removalImpact')}: {round.removalImpact}</p>}
      <section className="space-y-2"><h3 className={subhead}>{t('review.manifest')}</h3><ul className="space-y-2">{d.manifest.filter(m => m.roundId === round.id).map(m => <li key={m.sourceRevisionId}><SourceLink source={m.source} /></li>)}</ul><p className="text-xs/[18px] text-muted-foreground">{t('handoff.manualRevision')}</p></section>
      <section className="space-y-2"><h3 className={subhead}>{t('review.requiredDisciplines')}</h3><div className="grid gap-3 sm:grid-cols-2">{d.assignments.filter(a => a.roundId === round.id).map(a => <div key={a.id} className="space-y-2 rounded-lg border p-4">
        <div className="flex flex-wrap items-center justify-between gap-2"><h4 className="font-semibold">{discName(o, a.projectDisciplineId)}</h4><CoordStatus status={a.status} /></div>
        <p className="flex flex-wrap items-center gap-x-2 gap-y-1"><PersonLabel options={o} id={a.reviewerId} /><span className="text-muted-foreground tabular-nums">· {fmtDate(a.dueDate)}</span></p>
        {a.rationale && <p className="whitespace-pre-wrap">{a.rationale}</p>}
        <div className="flex flex-wrap gap-2">{live && round.startedAt && a.reviewerId === d.actorId && <Button size="sm" onClick={() => setAction({ type: 'decision', assignment: a })}>{t('review.recordDecision')}</Button>}{live && d.canCoordinate && <Button size="sm" variant="outline" onClick={() => setAction({ type: 'reviewer', assignment: a })}>{t('review.assignReviewer')}</Button>}</div>
      </div>)}</div></section>
      <section className="space-y-3"><div className="flex items-center justify-between gap-3"><h3 className={subhead}>{t('review.findings')}</h3>{live && round.startedAt && d.assignments.some(a => a.roundId === round.id && a.reviewerId === d.actorId) && <Button size="sm" onClick={() => setAction({ type: 'finding' })}>{t('review.addFinding')}</Button>}</div>
        {!d.findings.some(f => f.roundId === round.id) && <p className="text-muted-foreground">{t('review.noFindings')}</p>}
        {d.findings.filter(f => f.roundId === round.id).map(f => <article key={f.id} className="space-y-2 rounded-lg border p-4">
          <div className="flex flex-wrap items-center gap-2"><CoordStatus status={f.severity} /><CoordStatus status={f.status} /></div>
          <p className="whitespace-pre-wrap">{f.text}</p>
          {f.issueId && <p>{t('review.linkedIssue')}: {issues.data?.find(i => i.id === f.issueId) ? <Link className="text-primary underline underline-offset-4" to={`/projects/${number}/issues?panel=Issue:${f.issueId}`}><span className="key">{issues.data.find(i => i.id === f.issueId)!.key}</span></Link> : <span className="text-muted-foreground">{issues.isPending ? t('review.issueLoading') : t('review.issueUnavailable')}</span>}</p>}
          {f.carriedFromId && <p className="text-xs/[18px] text-muted-foreground">{t('review.carried')}</p>}
          <dl className="grid grid-cols-[minmax(8rem,auto)_1fr] gap-x-4 gap-y-2">
            <dt className="text-muted-foreground">{t('review.resolver')}</dt><dd><PersonLabel options={o} id={f.resolverId} /></dd>
            <dt className="text-muted-foreground">{t('review.verifier')}</dt><dd><PersonLabel options={o} id={f.verifierId} /></dd>
          </dl>
          {f.response && <p className="whitespace-pre-wrap">{f.response}</p>}{f.evidenceUrl && <a className="text-primary underline underline-offset-4" href={f.evidenceUrl} target="_blank" rel="noopener noreferrer">{t('coord.evidence')}</a>}{f.withdrawalAcknowledgedBy && <p>{t('review.withdrawalAcknowledged')}</p>}
          {live && <div className="flex flex-wrap gap-2">{f.resolverId === d.actorId && f.status === 'Open' && <Button size="sm" onClick={() => setAction({ type: 'Responded', finding: f })}>{t('review.respond')}</Button>}{f.verifierId === d.actorId && <>{f.status === 'Responded' && <><Button size="sm" onClick={() => setAction({ type: 'Verified Closed', finding: f })}>{t('review.verify')}</Button><Button size="sm" variant="outline" onClick={() => setAction({ type: 'Open', finding: f })}>{t('review.return')}</Button></>}{['Open', 'Responded'].includes(f.status) && <Button size="sm" variant="outline" onClick={() => setAction({ type: 'Withdrawn', finding: f })}>{t('review.withdraw')}</Button>}</>}{f.status === 'Withdrawn' && f.severity === 'Blocking' && !f.withdrawalAcknowledgedBy && p.coordinatorId === d.actorId && <Button size="sm" onClick={() => setAction({ type: 'acknowledgeWithdrawal', finding: f })}>{t('review.ackWithdrawal')}</Button>}{['Open', 'Responded'].includes(f.status) && <>{d.canCoordinate && <Button size="sm" variant="outline" onClick={() => setAction({ type: 'assignResolver', finding: f })}>{t('review.assignResolver')}</Button>}{d.canReassignVerifier && <Button size="sm" variant="outline" onClick={() => setAction({ type: 'assignVerifier', finding: f })}>{t('review.assignVerifier')}</Button>}</>}</div>}
          <details className="border-t pt-2"><summary className="cursor-pointer py-1 font-medium">{t('coord.history')}</summary><ol className="mt-2 space-y-2">{d.events.filter(e => e.findingId === f.id).map(e => <li key={e.id}><span className="font-medium">{tv(e.action)}</span> · {personName(o, e.createdBy)} · <span className="tabular-nums">{new Date(e.createdAt).toLocaleString()}</span><p className="whitespace-pre-wrap">{e.reason}</p></li>)}</ol></details>
        </article>)}
      </section>
    </div>}
  </DialogContent></Dialog>{action && d && p && <ReviewCommand c={c} d={d} action={action} close={() => setAction(null)} done={done} />}</>
}
function ReviewCommand({ c, d, action, close, done }: { c: RegisterContext & { id: string }; d: Detail; action: { type: string; assignment?: Assignment; finding?: Finding }; close: () => void; done: () => void }) {
  const { type, assignment: a, finding: f } = action, p = d.package, o = c.options, base = `projects/${c.projectId}/reviews/${p.id}`
  const issues = useQuery({ queryKey: ['review-issues', c.projectId], queryFn: () => get<ReviewIssue[]>(`projects/${c.projectId}/issues`), enabled: type === 'finding', staleTime: 30_000 })
  let path = `${base}/action`, title = t(`review.command.${type}`), fields: FormField[] = [reason], initial: Record<string, string> = {}, build: (v: Record<string, string>) => object = v => ({ rowVersion: p.rowVersion, action: type, reason: v.reason })
  if (type === 'start') fields = []
  if (type === 'coordinator' || type === 'reviewer') { path = type === 'reviewer' ? `${base}/assignments/${a!.id}/assign` : `${base}/assign`; fields = [{ name: 'ownerId', label: type === 'reviewer' ? 'review.reviewer' : 'coord.coordinator', choices: peopleChoices(o) }, reason]; build = v => ({ ...v, rowVersion: a?.rowVersion ?? p.rowVersion }) }
  if (type === 'decision') { path = `${base}/assignments/${a!.id}/decision`; fields = [{ name: 'status', label: 'common.status', choices: ['In Review', 'Changes Required', 'Approved'].map(s => ({ value: s, label: tv(s) })) }, { name: 'rationale', label: 'coord.rationale', type: 'textarea' }]; initial = { status: a!.status === 'Pending' ? 'In Review' : a!.status }; build = v => ({ ...v, rowVersion: a!.rowVersion }) }
  if (type === 'finding') { path = `${base}/findings`; fields = [{ name: 'sourceRevisionId', label: 'review.source', choices: d.manifest.filter(m => m.roundId === p.currentRoundId).map(m => ({ value: m.sourceRevisionId, label: `${m.source.sourceKey} · ${m.source.revision}` })) }, { name: 'projectDisciplineId', label: 'coord.discipline', choices: disciplineChoices(o).filter(x => d.assignments.some(a => a.roundId === p.currentRoundId && a.reviewerId === d.actorId && a.projectDisciplineId === x.value)) }, { name: 'resolverId', label: 'review.resolver', choices: peopleChoices(o) }, { name: 'severity', label: 'review.severity', choices: ['Blocking', 'Advisory'].map(s => ({ value: s, label: tv(s) })) }, { name: 'issueId', label: 'review.linkedIssue', optional: true, choices: (issues.data ?? []).map(i => ({ value: i.id, label: `${i.key} · ${i.title} · ${tv(i.status)}` })) }, { name: 'text', label: 'review.findingText', type: 'textarea' }]; initial = { severity: 'Blocking' }; build = v => ({ ...v, rowVersion: p.rowVersion, ...(v.issueId ? { issueId: v.issueId } : {}) }) }
  if (f) { path = `${base}/findings/${f.id}/action`; if (type === 'Responded') fields.push({ name: 'evidenceUrl', label: 'coord.evidenceUrl', type: 'url' }); if (type.startsWith('assign')) fields.unshift({ name: 'ownerId', label: type === 'assignVerifier' ? 'review.verifier' : 'review.resolver', choices: peopleChoices(o) }); build = v => ({ ...v, rowVersion: f.rowVersion, action: type }) }
  return <FieldsCommand path={path} title={title} fields={fields} initial={initial} build={build} onClose={close} onDone={done} />
}
