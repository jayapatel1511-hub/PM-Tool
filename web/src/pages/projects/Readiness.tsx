import { ViewMenu } from '@/components/hub/views'
import { AlertTriangle, CalendarCheck, ExternalLink, Plus } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Empty, ErrorBanner, Field, FilterBar, Loading, Page, Section } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { get } from '@/lib/api'
import { addDays, fmtDate, fmtTime, today } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { Chip, Key } from '@/components/hub/pills'
import { useMe } from '@/lib/auth'
import { cn } from '@/lib/utils'
import { useCurrentProject } from './ProjectLayout'
import { CommandForm, CoordStatus, PersonLabel, SelectField, personName, workChoices, workRef, type CoordOptions } from './CoordinationForms'
import { ReadinessInspector } from './ReadinessForms'
import { ExportMenu } from '@/components/hub/export'

type Commitment = {
  id: string; key: string; targetType: string; targetId: string; performerId: string; weekStart: string; targetDate: string
  intendedOutput: string; completionCriteria: string; state: string; snapshotId?: string | null; readinessAtCommit?: string | null
  rowVersion: number; completionEvidenceUrl?: string
}
type Snapshot = { id: string; weekStart: string; capturedAt: string; committedCount: number; met: number; withdrawn: number }
type Weekly = { commitments: Commitment[]; total: number; page: number; pageSize: number; truncated: boolean; snapshots: Snapshot[] }
type Constraint = { id: string; targetType: string; targetId: string; description: string; category: string; neededBy: string; sourceUrl: string }
type ReadyOutput = { id: string; targetType: string; targetId: string; key: string; name: string; dueDate: string | null; intendedOutput: string; completionCriteria: string; state: string }
type Aggregate = { constraints: Constraint[]; constraintsTotal: number; constraintsTruncated: boolean; readyOutputs: ReadyOutput[]; readyOutputsTotal: number; readyOutputsTruncated: boolean; pageSize: number }

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

const itemLink = 'font-semibold text-primary underline-offset-4 hover:underline'
const Truncated = ({ children }: { children: string }) => <p role="status" className="flex gap-2 border-b border-warn/30 bg-warn-bg px-5 py-2 text-xs/[18px] text-warn"><span aria-hidden>▲</span><span>{children}</span></p>

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
  const promisePage = Math.max(1, Number(sp.get('promisePage')) || 1)
  const constraintsPage = Math.max(1, Number(sp.get('constraintsPage')) || 1), readyPage = Math.max(1, Number(sp.get('readyPage')) || 1)
  const set = (key: string, value: string) => {
    const next = new URLSearchParams(sp)
    if (value) next.set(key, value); else next.delete(key)
    if (key === 'from' || key === 'to') for (const page of ['promisePage', 'constraintsPage', 'readyPage']) next.delete(page)
    setSp(next, { replace: true })
  }
  const reset = () => setSp(new URLSearchParams(), { replace: true })
  const clear = (...keys: string[]) => { const next = new URLSearchParams(sp); for (const key of keys) next.delete(key); setSp(next, { replace: true }) }
  // Search and key links open a promise or a constraint's work inspector: ?panel=OutputCommitment:id or WorkConstraint:id.
  const panel = sp.get('panel') ?? ''
  const promiseId = sp.get('promise') ?? (panel.startsWith('OutputCommitment:') ? panel.slice(17) : null)
  const constraintId = panel.startsWith('WorkConstraint:') ? panel.slice(15) : null
  const linked = useQuery({ queryKey: ['readiness-constraint', p.id, constraintId], enabled: !!constraintId,
    queryFn: () => get<{ targetType: string; targetId: string }>(`projects/${p.id}/readiness/constraints/${constraintId}`) })
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
    queryKey: ['p', p.id, 'readiness-window', from, to, promisePage, constraintsPage, readyPage],
    enabled: !invalidWindow,
    queryFn: async () => {
      // A promise due in the window starts at most six days earlier; rows keep the week start they were recorded with.
      const [aggregate, weekly] = await Promise.all([
        get<Aggregate>(`projects/${p.id}/readiness/window?from=${from}&to=${to}&constraintsPage=${constraintsPage}&readyPage=${readyPage}&pageSize=50`),
        get<Weekly>(`projects/${p.id}/weekly-commitments?from=${addDays(from, -6)}&to=${to}&targetFrom=${from}&targetTo=${to}&page=${promisePage}`),
      ])
      return { ...aggregate, ...weekly }
    },
  })
  const header = { title: t('readiness.title'), subtitle: t('readiness.subtitle') }
  // The window fields stay first in every state, so editing a date never unmounts the field being edited.
  const filters = <FilterBar><div className="flex flex-wrap items-end gap-3">
    <Field label={t('readiness.from')} htmlFor="readiness-from" className="w-full sm:w-44"><Input id="readiness-from" type="date" value={from} onChange={(e) => set('from', e.target.value)} /></Field>
    <Field label={t('readiness.to')} htmlFor="readiness-to" className="w-full sm:w-44"><Input id="readiness-to" type="date" value={to} onChange={(e) => set('to', e.target.value)} /></Field>
    <Button variant="link" className="px-1" onClick={reset}>{t('common.clear')}</Button>
    <p className="w-full self-center text-xs/[18px] text-muted-foreground sm:ml-auto sm:w-auto">{t('readiness.windowNote', { n: lookahead })}</p>
  </div></FilterBar>
  if (invalidWindow) return <Page {...header}>
    {filters}<div role="alert" className="flex items-center gap-3 rounded-md border border-bad/30 bg-bad-bg px-4 py-3 text-sm text-bad"><AlertTriangle className="size-4 shrink-0" aria-hidden />{t('readiness.invalidWindow')}</div>
  </Page>
  if (q.isPending) return <Page {...header}>{filters}<div className="rounded-lg border bg-card"><Loading rows={8} /></div></Page>
  if (q.error) return <Page {...header}>{filters}<ErrorBanner error={q.error} retry={() => q.refetch()} /></Page>
  const data = q.data
  const shown = data.commitments
  const snapshotByWeek = new Map(data.snapshots.map((s) => [s.weekStart, s]))
  // Weeks recorded on an earlier coordination day are shown beside the current ones, never re-dated.
  const sections = [...new Set([...weeks, ...shown.map((c) => c.weekStart), ...data.snapshots.map((s) => s.weekStart)])].sort()
  const base = `/projects/${p.projectNumber}`
  const href = (type: string, id: string) => `${base}/${type === 'Task' ? 'tasks' : 'deliverables'}?panel=${type}:${id}`
  // The linked work's readable key and name, when this viewer's options include it.
  const work = (type: string, id: string) => { const w = options.data && workRef(options.data, type, id); return w ? <><span className="key font-normal">{w.key}</span> · {w.name}</> : null }
  const canCapture = p.permissions.isPm && options.data?.canWrite
  return (
    <Page {...header} actions={
      <><ViewMenu listType="readiness" projectId={p.id} />{options.data && <Button variant="outline" onClick={() => setInspecting(true)}>{t('readiness.inspect')}</Button>}
      <ExportMenu path={`projects/${p.id}/weekly-commitments/export`} params={{ from, to }} name={`${p.projectNumber}-weekly-commitments`} label={t('readiness.exportPromises')} />
      <Button asChild variant="outline"><Link to={`${base}/coordination?meeting=1`}><CalendarCheck className="size-4" />{t('readiness.meeting')}</Link></Button>
      {options.data?.canWrite && <Button onClick={() => setProposing(true)}><Plus className="size-4" />{t('readiness.propose')}</Button>}</>
    }>
      {filters}
      {options.error && <ErrorBanner error={options.error} retry={() => options.refetch()} />}
      {linked.error && <ErrorBanner error={linked.error} />}
      <div className="grid gap-6 lg:grid-cols-2">
        <Section title={t('readiness.constraints')} id="constraints" count={data.constraintsTotal}
          actions={<ExportMenu path={`projects/${p.id}/readiness/window/export`} params={{ from, to, list: 'constraints' }} name={`${p.projectNumber}-readiness-constraints`} />}>
          {data.constraintsTruncated && <Truncated>{t('readiness.aggregateTruncated')}</Truncated>}
          {data.constraints.length === 0 ? <Empty>{t('readiness.noConstraints')}</Empty> : <ul className="divide-y">{data.constraints.map((c) => <li key={c.id} className="space-y-1 px-5 py-3 text-sm">
            <div className="flex flex-wrap items-center gap-2"><span className="rounded-md bg-secondary px-2 py-0.5 text-xs/[18px] font-medium">{tv(c.category)}</span>
              <Link className={itemLink} to={href(c.targetType, c.targetId)}>{work(c.targetType, c.targetId) ?? c.targetType}</Link></div>
            <p>{c.description}</p><p className="text-xs/[18px] text-muted-foreground"><span className="tabular-nums">{fmtDate(c.neededBy)}</span> · <a className="underline underline-offset-4" href={c.sourceUrl} target="_blank" rel="noreferrer">{t('readiness.source')}</a></p>
          </li>)}</ul>}
          <WindowPager className="border-t" label={t('readiness.constraints')} page={constraintsPage} total={data.constraintsTotal} pageSize={data.pageSize} onPage={n => set('constraintsPage', String(n))} />
        </Section>
        <Section title={t('readiness.readyOutputs')} id="ready-outputs" count={data.readyOutputsTotal}
          actions={<ExportMenu path={`projects/${p.id}/readiness/window/export`} params={{ from, to, list: 'ready' }} name={`${p.projectNumber}-ready-outputs`} />}>
          {data.readyOutputsTruncated && <Truncated>{t('readiness.aggregateTruncated')}</Truncated>}
          {data.readyOutputs.length === 0 ? <Empty>{t('readiness.noReadyOutputs')}</Empty> : <ul className="divide-y">{data.readyOutputs.map((o) => <li key={o.id} className="space-y-1 px-5 py-3 text-sm">
            <div className="flex flex-wrap items-center gap-2"><Link className={itemLink} to={href(o.targetType, o.targetId)}><span className="key font-normal">{o.key}</span> · {o.name}</Link><CoordStatus status={o.state} /></div>
            <p>{o.intendedOutput}</p><p className="text-xs/[18px] text-muted-foreground">{o.dueDate ? <span className="tabular-nums">{fmtDate(o.dueDate)}</span> : t('readiness.noDueDate')} · {o.completionCriteria}</p>
          </li>)}</ul>}
          <WindowPager className="border-t" label={t('readiness.readyOutputs')} page={readyPage} total={data.readyOutputsTotal} pageSize={data.pageSize} onPage={n => set('readyPage', String(n))} />
        </Section>
      </div>
      {data.truncated && <p role="status" className="flex gap-2 rounded-md border border-warn/40 bg-warn-bg px-4 py-3 text-sm text-warn"><span aria-hidden>▲</span><span>{t('readiness.truncated', { n: data.total })}</span></p>}
      {shown.length === 0 && !data.truncated && <div className="rounded-lg border bg-card"><Empty>{t('readiness.empty')}</Empty></div>}
      {sections.map((week) => {
        const rows = shown.filter((c) => c.weekStart === week)
        const snapshot = snapshotByWeek.get(week)
        return <Section key={week} title={t('readiness.week', { date: fmtDate(week) })} count={rows.length}
          actions={!snapshot && canCapture ? <Button size="sm" variant="outline" onClick={() => setSnapshotWeek(week)}>{t('readiness.capture')}</Button> : undefined}>
          {snapshot && <div className="flex flex-wrap gap-x-4 gap-y-1 border-b bg-muted px-5 py-2 text-xs/[18px] text-muted-foreground tabular-nums">
            <span>{t('readiness.snapshot', { n: snapshot.committedCount })}</span>
            <span>{t('readiness.met', { n: snapshot.met, total: snapshot.committedCount })}</span>
            {snapshot.withdrawn > 0 && <span>{t('readiness.withdrawn', { n: snapshot.withdrawn })}</span>}
          </div>}
          {rows.length === 0 ? <p className="px-5 py-3 text-sm text-muted-foreground">{t('readiness.noWeekCommitments')}</p> : <ul className="divide-y">{rows.map((c) => <li key={c.id} className="flex flex-wrap items-start gap-x-4 gap-y-2 px-5 py-3 text-sm">
            <div className="min-w-0 flex-1 space-y-1"><div className="flex flex-wrap items-center gap-2"><Key>{c.key}</Key><Link className={itemLink} to={href(c.targetType, c.targetId)}>{work(c.targetType, c.targetId) ?? <>{c.targetType} <span className="key font-normal">{c.targetId.slice(0, 8)}</span></>}</Link></div>
              <p>{c.intendedOutput}</p><p className="text-xs/[18px] text-muted-foreground">{t('readiness.criteria')}: {c.completionCriteria}</p></div>
            <div className="flex shrink-0 flex-wrap items-center gap-2"><span className="text-muted-foreground tabular-nums">{fmtDate(c.targetDate)}</span><CoordStatus status={c.state} />{c.readinessAtCommit ? <Chip tone={c.readinessAtCommit === 'Ready' ? 'done' : 'idle'}>{tv(c.readinessAtCommit)}</Chip> : <Chip tone="idle">{t('readiness.notRecorded')}</Chip>}</div>
            <Button size="sm" variant="outline" onClick={() => set('promise', c.id)}>{t('readiness.reviewPromise')}</Button>
          </li>)}</ul>}
        </Section>
      })}
      <WindowPager className="rounded-lg border bg-card" label={t('readiness.promises')} page={promisePage} total={data.total} pageSize={data.pageSize} onPage={n => set('promisePage', String(n))} />
      <p className="flex items-center gap-1.5 text-xs/[18px] text-muted-foreground"><ExternalLink className="size-3.5 shrink-0" aria-hidden />{t('readiness.sourceNote')}</p>
      {proposing && options.data && <ProposePromise projectId={p.id} options={options.data} week={weeks[0]} day={day}
        complete={p.status === 'Complete'} close={() => setProposing(false)} done={done} />}
      {(inspecting || linked.data) && options.data && <ReadinessInspector projectId={p.id} number={p.projectNumber} options={options.data}
        initial={linked.data && !inspecting ? `${linked.data.targetType}:${linked.data.targetId}` : undefined}
        close={() => { setInspecting(false); clear('panel') }} done={done} />}
      {snapshotWeek && <SnapshotForm projectId={p.id} week={snapshotWeek} close={() => setSnapshotWeek(null)} done={done} />}
      {promiseId && options.data && <PromiseDetail projectId={p.id} id={promiseId} options={options.data}
        close={() => clear('promise', 'panel')} done={done} />}
    </Page>
  )
}

function WindowPager({ label, page, total, pageSize, onPage, className }: { label: string; page: number; total: number; pageSize: number; onPage: (page: number) => void; className?: string }) {
  return <nav aria-label={label} className={cn('flex flex-wrap items-center justify-end gap-3 px-5 py-3', className)}>
    <span className="mr-auto text-sm text-muted-foreground tabular-nums">{t('coord.count', { n: total })}</span>
    <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => onPage(page - 1)}>{t('handoff.previous')}</Button>
    <span className="text-sm tabular-nums">{t('common.pageOf', { page, pages: Math.max(1, Math.ceil(total / Math.max(1, pageSize))) })}</span>
    <Button size="sm" variant="outline" disabled={page * pageSize >= total} onClick={() => onPage(page + 1)}>{t('handoff.next')}</Button>
  </nav>
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
    {work && <p className="flex flex-wrap items-center gap-2 text-sm"><span className="text-muted-foreground">{t('readiness.performer')}:</span><PersonLabel options={options} id={work.ownerId} /></p>}
    <div className="grid gap-3 sm:grid-cols-2">
      <Field label={t('readiness.weekStart')} htmlFor="promise-week" hint={t('readiness.weekStartHint', { day: t(`day.${day}`) })}>
        <Input id="promise-week" type="date" required min={week} step={7} value={start} onChange={e => { setStart(e.target.value); setTargetDate(e.target.value) }} /></Field>
      <Field label={t('readiness.targetDate')} htmlFor="promise-date"><Input id="promise-date" type="date" required min={start} max={dateValue(start) === null ? undefined : addDays(start, 6)} value={targetDate} onChange={e => setTargetDate(e.target.value)} /></Field>
    </div>
    <Field label={t('readiness.output')} htmlFor="promise-output"><Textarea id="promise-output" required maxLength={2000} value={output} onChange={e => setOutput(e.target.value)} /></Field>
    <Field label={t('readiness.criteria')} htmlFor="promise-criteria"><Textarea id="promise-criteria" required maxLength={2000} value={criteria} onChange={e => setCriteria(e.target.value)} /></Field>
    {complete && <Field label={t('common.reason')} htmlFor="promise-reason" hint={t('settings.correctionHint')}><Textarea id="promise-reason" required minLength={5} maxLength={4000} value={reason} onChange={e => setReason(e.target.value)} /></Field>}
  </CommandForm>
}

function SnapshotForm({ projectId, week, close, done }: { projectId: string; week: string; close: () => void; done: () => void }) {
  const [reason, setReason] = useState('')
  return <CommandForm path={`projects/${projectId}/weekly-commitments/snapshot`} title={t('readiness.capture')}
    hint={t('readiness.captureHint')} onClose={close} onDone={done} payload={() => ({ weekStart: week, reason })}>
    <p className="rounded-md bg-muted px-4 py-3 text-sm font-medium">{t('readiness.week', { date: fmtDate(week) })}</p>
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
    {q.isPending ? <Loading rows={4} /> : q.error ? <ErrorBanner error={q.error} retry={() => q.refetch()} /> : row && q.data && <div className="space-y-5 text-sm">
      <div className="flex flex-wrap items-center gap-2"><Key>{row.key}</Key><CoordStatus status={row.state} /></div>
      <dl className="grid grid-cols-[minmax(8rem,auto)_1fr] gap-x-4 gap-y-2">
        <dt className="text-muted-foreground">{t('readiness.performer')}</dt><dd><PersonLabel options={options} id={row.performerId} /></dd>
        <dt className="text-muted-foreground">{t('readiness.output')}</dt><dd className="whitespace-pre-wrap">{row.intendedOutput}</dd>
        <dt className="text-muted-foreground">{t('readiness.criteria')}</dt><dd className="whitespace-pre-wrap">{row.completionCriteria}</dd>
        <dt className="text-muted-foreground">{t('readiness.targetDate')}</dt><dd className="tabular-nums">{fmtDate(row.targetDate)}</dd>
      </dl>
      {row.completionEvidenceUrl && <a className="text-primary underline underline-offset-4" href={row.completionEvidenceUrl} target="_blank" rel="noopener noreferrer">{t('basis.evidence')}</a>}
      {(q.data.canCommit || q.data.canRecordMet || q.data.canRecordNotMet || q.data.canWithdraw) && <div className="flex flex-wrap gap-2 border-t pt-4">
        {q.data.canCommit && <Button onClick={() => setAction('Committed')}>{t('readiness.sign')}</Button>}
        {q.data.canRecordMet && <Button onClick={() => setAction('Met')}>{t('readiness.recordMet')}</Button>}
        {q.data.canRecordNotMet && <Button variant="outline" onClick={() => setAction('Not Met')}>{t('readiness.recordNotMet')}</Button>}
        {q.data.canWithdraw && <Button variant="outline" onClick={() => setAction('Withdrawn')}>{t('readiness.withdrawPromise')}</Button>}
      </div>}
      <section className="space-y-2"><h3 className="text-base/6 font-semibold">{t('readiness.promiseHistory')}</h3>
        <ul className="space-y-2">{q.data.events.map(e => <li key={e.id} className="space-y-1 rounded-lg border p-4">
          <p><span className="font-medium">{tv(e.fromState)} → {tv(e.toState)}</span> · {personName(options, e.actorId)} · <span className="tabular-nums">{fmtTime(e.createdAt)}</span></p>
          <p className="whitespace-pre-wrap">{e.reason}</p>{e.evidenceUrl && <a className="text-primary underline underline-offset-4" href={e.evidenceUrl} target="_blank" rel="noopener noreferrer">{t('basis.evidence')}</a>}
        </li>)}</ul></section>
    </div>}
  </DialogContent></Dialog>
}

function PromiseMove({ projectId, row, state, close, done }: { projectId: string; row: Commitment; state: string; close: () => void; done: () => void }) {
  const [reason, setReason] = useState(''), [evidenceUrl, setEvidenceUrl] = useState('')
  const label = state === 'Committed' ? 'readiness.sign' : state === 'Met' ? 'readiness.recordMet' : state === 'Not Met' ? 'readiness.recordNotMet' : 'readiness.withdrawPromise'
  return <CommandForm path={`projects/${projectId}/weekly-commitments/${row.id}/transition`} title={t(label)}
    hint={t('readiness.promiseHint')} onClose={close} onDone={done} submitLabel={t(label)}
    payload={() => ({ rowVersion: row.rowVersion, toState: state, reason, evidenceUrl: evidenceUrl || null })}>
    <div className="space-y-1 rounded-md bg-muted px-4 py-3 text-sm"><p className="whitespace-pre-wrap">{row.intendedOutput}</p><p className="text-muted-foreground">{t('readiness.criteria')}: {row.completionCriteria}</p></div>
    <Field label={t('basis.reason')} htmlFor="promise-reason"><Textarea id="promise-reason" required minLength={5} value={reason} onChange={e => setReason(e.target.value)} /></Field>
    {state !== 'Committed' && <Field label={t('basis.evidence')} htmlFor="promise-evidence" optional={state !== 'Met'}><Input id="promise-evidence" type="url" required={state === 'Met'} value={evidenceUrl} onChange={e => setEvidenceUrl(e.target.value)} /></Field>}
  </CommandForm>
}
