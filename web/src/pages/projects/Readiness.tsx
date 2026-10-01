import { CalendarCheck, ExternalLink } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Empty, ErrorBanner, Field, Loading, Page, Section } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { get } from '@/lib/api'
import { addDays, fmtDate, fmtTime, today } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { Chip, StatusPill } from '@/components/hub/pills'
import { useMe } from '@/lib/auth'
import { useCurrentProject } from './ProjectLayout'
import { CommandForm, SelectField, personName, workChoices, workRef, type CoordOptions } from './CoordinationForms'
import { ReadinessInspector } from './ReadinessForms'
import { ExportMenu } from '@/components/hub/export'

type Commitment = {
  id: string; targetType: string; targetId: string; performerId: string; weekStart: string; targetDate: string
  intendedOutput: string; completionCriteria: string; state: string; snapshotId?: string | null; readinessAtCommit?: string | null
  rowVersion: number; completionEvidenceUrl?: string
}
type Snapshot = { id: string; weekStart: string; capturedAt: string; committedCount: number; met: number; withdrawn: number }
type Weekly = { commitments: Commitment[]; total: number; truncated: boolean; snapshots: Snapshot[] }
type Constraint = { id: string; targetType: string; targetId: string; description: string; category: string; neededBy: string; sourceUrl: string }
type ReadyOutput = { id: string; targetType: string; targetId: string; key: string; name: string; dueDate: string | null; intendedOutput: string; completionCriteria: string; state: string }
type Aggregate = { constraints: Constraint[]; constraintsTotal: number; constraintsTruncated: boolean; readyOutputs: ReadyOutput[]; readyOutputsTotal: number; readyOutputsTruncated: boolean }

const DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']
/** FR-RDY-05: promise weeks start on the project's coordination day (Monday when unset). */
function weekStart(d: string, day: string) {
  return addDays(d, -((new Date(`${d}T00:00:00Z`).getUTCDay() - DAYS.indexOf(day) + 7) % 7))
}

function dateValue(d: string) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(d)) return null
  const value = Date.parse(`${d}T00:00:00Z`)
  return Number.isFinite(value) && new Date(value).toISOString().slice(0, 10) === d ? value : null
}

/** Packet 032 readiness window and explicit weekly promise signatures. */
export function ReadinessTab() {
  const p = useCurrentProject()
  const qc = useQueryClient(), [proposing, setProposing] = useState(false), [snapshotWeek, setSnapshotWeek] = useState<string | null>(null)
  const [inspecting, setInspecting] = useState(false)
  const options = useQuery({ queryKey: ['coord-options', p.id], queryFn: () => get<CoordOptions>(`projects/${p.id}/changes/options`) })
  const lookahead = useMe().settings.coordinationLookaheadWeeks
  const [sp, setSp] = useSearchParams()
  const day = DAYS.includes(p.coordinationDay ?? '') ? p.coordinationDay! : 'Monday'
  const defaultFrom = weekStart(today(), day)
  const from = sp.get('from') ?? defaultFrom
  const to = sp.get('to') ?? addDays(defaultFrom, lookahead * 7 - 1)
  const set = (key: string, value: string) => {
    const next = new URLSearchParams(sp)
    if (value) next.set(key, value); else next.delete(key)
    setSp(next, { replace: true })
  }
  const reset = () => setSp(new URLSearchParams(), { replace: true })
  const done = () => { setProposing(false); setSnapshotWeek(null); qc.invalidateQueries({ queryKey: ['p', p.id, 'readiness-window'] }); qc.invalidateQueries({ queryKey: ['weekly-promise', p.id] }) }
  const weeks = useMemo(() => {
    const fromValue = dateValue(from)
    const toValue = dateValue(to)
    if (fromValue === null || toValue === null || toValue < fromValue || toValue - fromValue > 83 * 86400000) return []
    const first = weekStart(from, day)
    const last = weekStart(to, day)
    const result: string[] = []
    for (let w = first; w <= last; w = addDays(w, 7)) result.push(w)
    return result
  }, [from, to, day])
  const invalidWindow = weeks.length === 0 || weeks.length > 12
  const q = useQuery({
    queryKey: ['p', p.id, 'readiness-window', from, to],
    enabled: !invalidWindow,
    queryFn: async () => {
      // A promise due in the window starts at most six days earlier; rows keep the week start they were recorded with.
      const [aggregate, weekly] = await Promise.all([
        get<Aggregate>(`projects/${p.id}/readiness/window?from=${from}&to=${to}`),
        get<Weekly>(`projects/${p.id}/weekly-commitments?from=${addDays(from, -6)}&to=${to}`),
      ])
      return { ...aggregate, ...weekly }
    },
  })
  const filters = <div className="flex flex-wrap items-end gap-2 rounded-lg border bg-card p-3">
    <label className="text-xs text-muted-foreground">{t('readiness.from')}<Input type="date" className="mt-1 h-8 w-36" value={from} onChange={(e) => set('from', e.target.value)} /></label>
    <label className="text-xs text-muted-foreground">{t('readiness.to')}<Input type="date" className="mt-1 h-8 w-36" value={to} onChange={(e) => set('to', e.target.value)} /></label>
    <Button size="sm" variant="ghost" onClick={reset}>{t('common.clear')}</Button>
    <span className="ml-auto text-xs text-muted-foreground">{t('readiness.windowNote', { n: lookahead })}</span>
  </div>
  if (invalidWindow) return <Page title={t('readiness.title')} subtitle={t('readiness.subtitle')}>
    {filters}<div role="alert" className="rounded border border-bad/30 bg-bad-bg px-3 py-2 text-sm text-bad">{t('readiness.invalidWindow')}</div>
  </Page>
  if (q.isPending) return <Loading rows={8} />
  if (q.error) return <div className="p-6"><ErrorBanner error={q.error} retry={() => q.refetch()} /></div>
  const data = q.data
  const shown = data.commitments.filter((c) => c.targetDate >= from && c.targetDate <= to)
  const snapshotByWeek = new Map(data.snapshots.map((s) => [s.weekStart, s]))
  // Weeks recorded on an earlier coordination day are shown beside the current ones, never re-dated.
  const sections = [...new Set([...weeks, ...shown.map((c) => c.weekStart), ...data.snapshots.map((s) => s.weekStart)])].sort()
  const base = `/projects/${p.projectNumber}`
  const workLink = (c: Commitment) => `${base}/${c.targetType === 'Task' ? 'tasks' : 'deliverables'}?panel=${c.targetType}:${c.targetId}`
  return (
    <Page title={t('readiness.title')} subtitle={t('readiness.subtitle')} actions={
      <>{options.data && <Button size="sm" variant="outline" onClick={() => setInspecting(true)}>{t('readiness.inspect')}</Button>}
      {options.data?.canWrite && <Button size="sm" onClick={() => setProposing(true)}>{t('readiness.propose')}</Button>}
      <ExportMenu path={`projects/${p.id}/weekly-commitments/export`} params={{ from, to }} name={`${p.projectNumber}-weekly-commitments`} label={t('readiness.exportPromises')} />
      <Button asChild variant="outline" size="sm"><Link to={`${base}/coordination?meeting=1`}><CalendarCheck className="size-4" />{t('readiness.meeting')}</Link></Button></>
    }>
      {options.error && <ErrorBanner error={options.error} retry={() => options.refetch()} />}
      {filters}
      <div className="grid gap-4 md:grid-cols-2">
        <Section title={t('readiness.constraints')} id="constraints" count={data.constraintsTotal}
          actions={<ExportMenu path={`projects/${p.id}/readiness/window/export`} params={{ from, to, list: 'constraints' }} name={`${p.projectNumber}-readiness-constraints`} />}>
          {data.constraintsTruncated && <p role="status" className="border-b border-warn/30 bg-warn-bg px-4 py-2 text-xs text-warn">{t('readiness.aggregateTruncated')}</p>}
          {data.constraints.length === 0 ? <p className="px-4 py-4 text-sm text-muted-foreground">{t('readiness.noConstraints')}</p> : <ul className="divide-y">{data.constraints.map((c) => <li key={c.id} className="px-4 py-3 text-sm">
            <Link className="font-medium text-primary hover:underline" to={`${base}/${c.targetType === 'Task' ? 'tasks' : 'deliverables'}?panel=${c.targetType}:${c.targetId}`}>{c.category} · {c.targetType}</Link>
            <p className="mt-1">{c.description}</p><p className="text-xs text-muted-foreground">{fmtDate(c.neededBy)} · <a className="underline" href={c.sourceUrl} target="_blank" rel="noreferrer">{t('readiness.source')}</a></p>
          </li>)}</ul>}
        </Section>
        <Section title={t('readiness.readyOutputs')} id="ready-outputs" count={data.readyOutputsTotal}
          actions={<ExportMenu path={`projects/${p.id}/readiness/window/export`} params={{ from, to, list: 'ready' }} name={`${p.projectNumber}-ready-outputs`} />}>
          {data.readyOutputsTruncated && <p role="status" className="border-b border-warn/30 bg-warn-bg px-4 py-2 text-xs text-warn">{t('readiness.aggregateTruncated')}</p>}
          {data.readyOutputs.length === 0 ? <p className="px-4 py-4 text-sm text-muted-foreground">{t('readiness.noReadyOutputs')}</p> : <ul className="divide-y">{data.readyOutputs.map((o) => <li key={o.id} className="px-4 py-3 text-sm">
            <Link className="font-medium text-primary hover:underline" to={`${base}/${o.targetType === 'Task' ? 'tasks' : 'deliverables'}?panel=${o.targetType}:${o.targetId}`}>{o.key} · {o.name}</Link>
            <p className="mt-1">{o.intendedOutput}</p><p className="text-xs text-muted-foreground">{o.dueDate ? fmtDate(o.dueDate) : t('readiness.noDueDate')} · {o.completionCriteria}</p>
          </li>)}</ul>}
        </Section>
      </div>
      {data.truncated && <div role="status" className="rounded border border-warn/30 bg-warn-bg px-3 py-2 text-sm text-warn">{t('readiness.truncated', { n: data.total })}</div>}
      {shown.length === 0 && !data.truncated && <div className="rounded-lg border bg-card"><Empty>{t('readiness.empty')}</Empty></div>}
      {sections.map((week) => {
        const rows = shown.filter((c) => c.weekStart === week)
        const snapshot = snapshotByWeek.get(week)
        return <Section key={week} title={t('readiness.week', { date: fmtDate(week) })} count={rows.length}>
          {!snapshot && p.permissions.isPm && options.data?.canWrite && <div className="border-b px-4 py-2"><Button size="sm" variant="outline" onClick={() => setSnapshotWeek(week)}>{t('readiness.capture')}</Button></div>}
          {snapshot && <div className="flex flex-wrap gap-x-4 gap-y-1 border-b bg-muted/30 px-4 py-2 text-xs text-muted-foreground">
            <span>{t('readiness.snapshot', { n: snapshot.committedCount })}</span>
            <span>{t('readiness.met', { n: snapshot.met, total: snapshot.committedCount })}</span>
            {snapshot.withdrawn > 0 && <span>{t('readiness.withdrawn', { n: snapshot.withdrawn })}</span>}
          </div>}
          {rows.length === 0 ? <p className="px-4 py-3 text-sm text-muted-foreground">{t('readiness.noWeekCommitments')}</p> : <ul className="divide-y">{rows.map((c) => <li key={c.id} className="flex flex-wrap items-start gap-3 px-4 py-3 text-sm">
            <div className="min-w-0 flex-1"><Link className="font-medium text-primary hover:underline" to={workLink(c)}>{c.targetType} <span className="font-mono text-xs">{c.targetId.slice(0, 8)}</span></Link><p className="mt-1">{c.intendedOutput}</p><p className="text-xs text-muted-foreground">{t('readiness.criteria')}: {c.completionCriteria}</p></div>
            <div className="flex shrink-0 flex-wrap items-center gap-2"><span className="text-xs text-muted-foreground">{fmtDate(c.targetDate)}</span><StatusPill status={c.state} />{c.readinessAtCommit ? <Chip tone={c.readinessAtCommit === 'Ready' ? 'done' : 'idle'}>{tv(c.readinessAtCommit)}</Chip> : <Chip tone="idle">{t('readiness.notRecorded')}</Chip>}</div>
            <Button size="sm" variant="outline" onClick={() => set('promise', c.id)}>{t('readiness.reviewPromise')}</Button>
          </li>)}</ul>}
        </Section>
      })}
      <p className="text-xs text-muted-foreground"><ExternalLink className="mr-1 inline size-3" aria-hidden />{t('readiness.sourceNote')}</p>
      {proposing && options.data && <ProposePromise projectId={p.id} options={options.data} week={weeks[0]} day={day}
        complete={p.status === 'Complete'} close={() => setProposing(false)} done={done} />}
      {inspecting && options.data && <ReadinessInspector projectId={p.id} number={p.projectNumber} options={options.data} close={() => setInspecting(false)} done={done} />}
      {snapshotWeek && <SnapshotForm projectId={p.id} week={snapshotWeek} close={() => setSnapshotWeek(null)} done={done} />}
      {sp.get('promise') && options.data && <PromiseDetail projectId={p.id} id={sp.get('promise')!} options={options.data}
        close={() => set('promise', '')} done={done} />}
    </Page>
  )
}

function ProposePromise({ projectId, options, week, day, complete, close, done }: { projectId: string; options: CoordOptions; week: string; day: string
  complete: boolean; close: () => void; done: () => void }) {
  const [target, setTarget] = useState(''), [start, setStart] = useState(week), [targetDate, setTargetDate] = useState(week)
  const [output, setOutput] = useState(''), [criteria, setCriteria] = useState(''), [reason, setReason] = useState('')
  const [type, id] = target.split(':'), work = workRef(options, type, id)
  const eligible = { ...options, tasks: options.tasks.filter(w => w.ownerId === options.actorId || options.manageDisciplineIds.includes(w.projectDisciplineId)),
    deliverables: options.deliverables.filter(w => w.ownerId === options.actorId || options.manageDisciplineIds.includes(w.projectDisciplineId)) }
  return <CommandForm path={`projects/${projectId}/weekly-commitments/${type}/${id}`} title={t('readiness.propose')}
    hint={t('readiness.proposeHint')} onClose={close} onDone={done} submitLabel={t('common.save')}
    payload={() => { if (!work) throw new Error(t('coord.unavailable')); return { targetRowVersion: work.rowVersion, weekStart: start, targetDate,
      intendedOutput: output, completionCriteria: criteria, reason: reason || null } }}>
    <SelectField label={t('readiness.work')} value={target} onChange={setTarget} choices={workChoices(eligible)} />
    {work && <p>{t('readiness.performer')}: {personName(options, work.ownerId)}</p>}
    <Field label={t('readiness.weekStart')} htmlFor="promise-week" hint={t('readiness.weekStartHint', { day: t(`day.${day}`) })}>
      <Input id="promise-week" type="date" required min={week} step={7} value={start} onChange={e => { setStart(e.target.value); setTargetDate(e.target.value) }} /></Field>
    <Field label={t('readiness.targetDate')} htmlFor="promise-date"><Input id="promise-date" type="date" required min={start} max={dateValue(start) === null ? undefined : addDays(start, 6)} value={targetDate} onChange={e => setTargetDate(e.target.value)} /></Field>
    <Field label={t('readiness.output')} htmlFor="promise-output"><Textarea id="promise-output" required maxLength={2000} value={output} onChange={e => setOutput(e.target.value)} /></Field>
    <Field label={t('readiness.criteria')} htmlFor="promise-criteria"><Textarea id="promise-criteria" required maxLength={2000} value={criteria} onChange={e => setCriteria(e.target.value)} /></Field>
    {complete && <Field label={t('common.reason')} htmlFor="promise-reason" hint={t('settings.correctionHint')}><Textarea id="promise-reason" required minLength={5} maxLength={4000} value={reason} onChange={e => setReason(e.target.value)} /></Field>}
  </CommandForm>
}

function SnapshotForm({ projectId, week, close, done }: { projectId: string; week: string; close: () => void; done: () => void }) {
  const [reason, setReason] = useState('')
  return <CommandForm path={`projects/${projectId}/weekly-commitments/snapshot`} title={t('readiness.capture')}
    hint={t('readiness.captureHint')} onClose={close} onDone={done} payload={() => ({ weekStart: week, reason })}>
    <p>{t('readiness.week', { date: fmtDate(week) })}</p>
    <Field label={t('basis.reason')} htmlFor="snapshot-reason"><Textarea id="snapshot-reason" required minLength={5} value={reason} onChange={e => setReason(e.target.value)} /></Field>
  </CommandForm>
}

type PromiseInfo = { commitment: Commitment; events: { id: string; fromState: string; toState: string; reason: string; actorId: string; createdAt: string; evidenceUrl?: string }[];
  canCommit: boolean; canRecordMet: boolean; canRecordNotMet: boolean; canWithdraw: boolean }
function PromiseDetail({ projectId, id, options, close, done }: { projectId: string; id: string; options: CoordOptions; close: () => void; done: () => void }) {
  const q = useQuery({ queryKey: ['weekly-promise', projectId, id], queryFn: () => get<PromiseInfo>(`projects/${projectId}/weekly-commitments/${id}`) })
  const [action, setAction] = useState<string | null>(null)
  const row = q.data?.commitment
  const changed = () => { setAction(null); done(); q.refetch() }
  if (action && row) return <PromiseMove projectId={projectId} row={row} state={action} close={() => setAction(null)} done={changed} />
  return <Dialog open onOpenChange={o => !o && close()}><DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
    <DialogHeader><DialogTitle>{t('readiness.reviewPromise')}</DialogTitle><DialogDescription>{t('readiness.promiseHint')}</DialogDescription></DialogHeader>
    {q.isPending ? <Loading rows={4} /> : q.error ? <ErrorBanner error={q.error} retry={() => q.refetch()} /> : row && q.data && <div className="space-y-4 text-sm">
      <StatusPill status={row.state} /><p>{t('readiness.performer')}: {personName(options, row.performerId)}</p>
      <p>{t('readiness.output')}: {row.intendedOutput}</p><p>{t('readiness.criteria')}: {row.completionCriteria}</p>
      <p>{t('readiness.targetDate')}: {fmtDate(row.targetDate)}</p>
      {row.completionEvidenceUrl && <a className="text-primary underline" href={row.completionEvidenceUrl} target="_blank" rel="noopener noreferrer">{t('basis.evidence')}</a>}
      <div className="flex flex-wrap gap-2">
        {q.data.canCommit && <Button onClick={() => setAction('Committed')}>{t('readiness.sign')}</Button>}
        {q.data.canRecordMet && <Button onClick={() => setAction('Met')}>{t('readiness.recordMet')}</Button>}
        {q.data.canRecordNotMet && <Button variant="outline" onClick={() => setAction('Not Met')}>{t('readiness.recordNotMet')}</Button>}
        {q.data.canWithdraw && <Button variant="outline" onClick={() => setAction('Withdrawn')}>{t('readiness.withdrawPromise')}</Button>}
      </div>
      <h3 className="font-medium">{t('readiness.promiseHistory')}</h3>
      <ul className="space-y-2">{q.data.events.map(e => <li key={e.id} className="rounded border p-3">
        <p>{tv(e.fromState)} → {tv(e.toState)} · {personName(options, e.actorId)} · {fmtDate(e.createdAt)} {fmtTime(e.createdAt)}</p>
        <p>{e.reason}</p>{e.evidenceUrl && <a className="text-primary underline" href={e.evidenceUrl} target="_blank" rel="noopener noreferrer">{t('basis.evidence')}</a>}
      </li>)}</ul>
    </div>}
  </DialogContent></Dialog>
}

function PromiseMove({ projectId, row, state, close, done }: { projectId: string; row: Commitment; state: string; close: () => void; done: () => void }) {
  const [reason, setReason] = useState(''), [evidenceUrl, setEvidenceUrl] = useState('')
  const label = state === 'Committed' ? 'readiness.sign' : state === 'Met' ? 'readiness.recordMet' : state === 'Not Met' ? 'readiness.recordNotMet' : 'readiness.withdrawPromise'
  return <CommandForm path={`projects/${projectId}/weekly-commitments/${row.id}/transition`} title={t(label)}
    hint={t('readiness.promiseHint')} onClose={close} onDone={done} submitLabel={t(label)}
    payload={() => ({ rowVersion: row.rowVersion, toState: state, reason, evidenceUrl: evidenceUrl || null })}>
    <p>{row.intendedOutput}</p><p>{t('readiness.criteria')}: {row.completionCriteria}</p>
    <Field label={t('basis.reason')} htmlFor="promise-reason"><Textarea id="promise-reason" required minLength={5} value={reason} onChange={e => setReason(e.target.value)} /></Field>
    {state !== 'Committed' && <Field label={t('basis.evidence')} htmlFor="promise-evidence"><Input id="promise-evidence" type="url" required={state === 'Met'} value={evidenceUrl} onChange={e => setEvidenceUrl(e.target.value)} /></Field>}
  </CommandForm>
}
