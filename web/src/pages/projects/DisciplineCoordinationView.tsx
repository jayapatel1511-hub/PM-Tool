import { useQuery } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router'
import { useEffect, type ReactNode } from 'react'
import { ErrorBanner, Field, FilterBar, Loading, selectCls } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { useItemPanel } from '@/components/hub/panel-host'
import { get, qs } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { plural, t, tv } from '@/lib/i18n'
import type { ProjectDetail } from '@/lib/types'
import type { Accent } from '@/lib/utils'
import { CoordStatus } from './CoordinationForms'

type Handoff = { id: string; key: string; title: string; status: string; neededBy: string; promisedBy?: string; targetKey?: string; sendingOwnerId: string; receivingOwnerId: string; sendingDisciplineId: string; receivingDisciplineId: string }
export type ChangeCounts = { pendingAssessments: number; acknowledgedPending: number; projectPending: number }
type Change = ChangeCounts & { id: string; key: string; title: string; status: string; ownerId?: string }
type Review = { id: string; key: string; title: string; status: string; outstandingDisciplines: number; blockingFindings: number; coordinatorId?: string }
type LinkedIssue = { id: string; key: string; title: string; status: string; ownerId?: string | null; ownerName?: string | null; projectDisciplineId?: string | null }
type InputUse = { id: string; targetType: string; targetId: string; sourceRevisionId: string; targetKey?: string | null; targetName?: string | null; sourceKey?: string | null; sourceUrl?: string | null; revision?: string | null; intendedUse: string }
type BlockerGroup = { handoffId: string; handoffKey: string; taskIds: string[]; taskKeys: string[] }
type Startability = { id: string; targetType: 'Task' | 'Deliverable'; targetId: string; key: string; name: string;
  dueDate: string | null; state: string; blocked: string[]; unknown: string[] }
type LinkedAction = { id: string; key: string; text: string; status: string; dueDate: string | null;
  sourceType: 'Handoff' | 'ChangeNotice'; sourceId: string; targetType: 'Task' | 'Deliverable'; targetId: string }
type ChangeTarget = { changeNoticeId: string; targetType: 'Task' | 'Deliverable'; targetId: string }
type UnavailableChangeTarget = { changeNoticeId: string; count: number }
type UpcomingSubmission = { id: string; key: string; title: string; status: string; effectiveStatus: string; targetDate: string; coordinatorId: string; failingChecks: { sourceId?: string; kind: string; code: string; message: string; sourcePath?: string }[] }
type StaffingConflict = { id: string; key: string; targetType: string; targetId: string; description: string; neededBy: string; state: string; affectedOwnerId: string; removalOwnerId: string }
type Data = { handoffs: Handoff[]; outgoing: Handoff[]; incoming: Handoff[]; changes: Change[]; reviews: Review[]; linkedIssues: LinkedIssue[]; uses: InputUse[]; blockerGroups: BlockerGroup[]; usesTotal: number; linkedIssuesTotal: number; handoffsTotal: number; outgoingTotal: number; incomingTotal: number; outgoingPage: number; incomingPage: number; reviewsPage: number; reviewsPageSize: number; changesTotal: number; reviewsTotal: number; evaluatedAt: string;
  startability: Startability[]; startabilityReadyTotal: number; startabilityFrom: string; startabilityTo: string;
  linkedActions: LinkedAction[]; changeTargets: ChangeTarget[]; unavailableChangeTargets: UnavailableChangeTarget[];
  usesPage: number; usesPageSize: number; linkedIssuesPage: number; linkedIssuesPageSize: number; changesPage: number; changesPageSize: number;
  startabilityTotal: number; startabilityPage: number; startabilityPageSize: number; upcomingSubmissions: UpcomingSubmission[]; upcomingSubmissionsTotal: number;
  staffingConflicts: StaffingConflict[]; staffingConflictsTotal: number; upcomingSubmissionsPage: number; staffingConflictsPage: number; blockerGroupsPage: number; handoffPage: number; handoffPageSize: number; blockerGroupsTotal: number; page: number; pageSize: number }

/** Each of the five questions keeps one identity stripe wherever it appears; the stripe never signals status. */
const QUESTION_ACCENT: Record<string, Accent> = { owe: 'blue', waiting: 'mint', using: 'lavender', changed: 'peach', start: 'amber' }
const box = 'overflow-hidden rounded-lg border bg-card', head = 'flex flex-wrap items-start justify-between gap-2 px-4 pb-2 pt-3', body = 'px-4 pb-3 text-sm'
const badge = 'rounded-md bg-secondary px-2 py-0.5 text-xs font-medium text-muted-foreground tabular-nums', title = 'text-base/6 font-semibold'
const keyLink = 'key text-primary underline underline-offset-4', textLink = 'text-primary underline underline-offset-4'
const row = 'space-y-1 py-2 first:pt-0 last:pb-0'

function Pager({ label, page, total, pageSize, onPage }: { label: string; page: number; total: number; pageSize: number; onPage: (page: number) => void }) {
  if (pageSize <= 0 || total <= pageSize) return null
  return <nav className="no-print mt-3 flex flex-wrap items-center justify-end gap-2" aria-label={label}>
    <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => onPage(page - 1)}>{t('handoff.previous')}</Button>
    <span className="text-xs tabular-nums" aria-live="polite">{t('common.pageOf', { page, pages: Math.ceil(total / pageSize) })}</span>
    <Button size="sm" variant="outline" disabled={page * pageSize >= total} onClick={() => onPage(page + 1)}>{t('handoff.next')}</Button>
  </nav>
}

/** A flagged list of names (blocked ■ / unknown ▲): the reason stays readable, never colour alone. */
const Flags = ({ tone, children }: { tone: 'bad' | 'warn'; children: string }) =>
  <span className={tone === 'bad' ? 'text-bad' : 'text-warn'}><span aria-hidden>{tone === 'bad' ? '■ ' : '▲ '}</span>{children}</span>

/** FR-DCV-03: Pending Assessment is shown apart from acknowledgement; a scoped count also states the whole-project count. */
export function ChangeAssessmentCounts({ c, scoped }: { c: ChangeCounts; scoped: boolean }) {
  const n = c.pendingAssessments
  return <>{t(scoped ? 'dcv.pendingAssessmentScoped' : 'dcv.pendingAssessment', { n })}
    {n > 0 && ` · ${t('dcv.pendingAcknowledged', { ack: c.acknowledgedPending, n })}`}
    {scoped && c.projectPending !== n && ` · ${t('dcv.projectPending', { n: c.projectPending })}`}</>
}

/** Packet 030's five-question coordination projection over the existing registers. */
export function DisciplineCoordinationView({ project, disciplineId, meeting, canCapture, onCapture, printAll, onPrinted }: {
  project: ProjectDetail; disciplineId?: string; meeting?: boolean; canCapture?: boolean; printAll?: boolean; onPrinted?: (printing: boolean) => void;
  onCapture?: (label: string, links: { targetType: string; targetId: string }[], linkedActionIds: string[]) => void
}) {
  const openPanel = useItemPanel()
  const [sp, setSp] = useSearchParams()
  const ownerId = sp.get('owner') ?? ''
  const from = sp.get('from') ?? ''
  const to = sp.get('to') ?? ''
  const page = Math.max(1, Number(sp.get('rowsPage') ?? 1) || 1)
  const setScope = (name: string, value: string) => setSp(p => {
    const next = new URLSearchParams(p)
    if (value) next.set(name, value); else next.delete(name)
    next.delete('rowsPage')
    return next
  }, { replace: true })
  const setPage = (next: number) => setSp(p => { const n = new URLSearchParams(p); if (next > 1) n.set('rowsPage', String(next)); else n.delete('rowsPage'); return n }, { replace: true })
  const clearScope = () => setSp(p => { const next = new URLSearchParams(p); ['owner', 'from', 'to', 'rowsPage'].forEach(name => next.delete(name)); return next }, { replace: true })
  const team = useQuery({ queryKey: ['p', project.id, 'team'], queryFn: () => get<{ members: { userId: string; displayName: string }[] }>(`projects/${project.id}/team`), staleTime: 60_000 })
  const q = useQuery({
    queryKey: ['p', project.id, 'discipline-coordination', disciplineId, ownerId, from, to, page, printAll],
    staleTime: 0,
    queryFn: () => get<Data>(`projects/${project.id}/discipline-coordination${qs({ disciplineId, ownerId, from, to, page: printAll ? undefined : page, pageSize: printAll ? undefined : 4 })}`),
  })
  useEffect(() => {
    if (!printAll || q.isPending || q.isFetching || !onPrinted) return
    if (q.error) { onPrinted(false); return }
    const frame = requestAnimationFrame(() => { try { window.print() } finally { onPrinted(false) } })
    return () => cancelAnimationFrame(frame)
  }, [printAll, q.isPending, q.isFetching, q.error, onPrinted])
  const filters = <FilterBar><div role="group" className="flex flex-wrap items-end gap-3" aria-label={t('dcv.scopeLabel')}>
    <p className="w-full text-sm text-muted-foreground">{t('dcv.projectPrefix')} <strong className="font-semibold text-foreground">{project.projectNumber}</strong>{disciplineId ? ` · ${project.disciplines.find(x => x.id === disciplineId)?.name ?? t('dcv.selectedDiscipline')}` : ''}</p>
    <Field label={t('common.owner')} htmlFor="dcv-owner" className="w-full sm:w-56"><select id="dcv-owner" className={selectCls} value={ownerId} onChange={e => setScope('owner', e.target.value)}><option value="">{t('dcv.allPermittedOwners')}</option>{team.data?.members.map(m => <option key={m.userId} value={m.userId}>{m.displayName}</option>)}</select></Field>
    <Field label={t('common.from')} htmlFor="dcv-from" className="w-full sm:w-44"><Input id="dcv-from" type="date" value={from} onChange={e => setScope('from', e.target.value)} /></Field>
    <Field label={t('common.to')} htmlFor="dcv-to" className="w-full sm:w-44"><Input id="dcv-to" type="date" value={to} onChange={e => setScope('to', e.target.value)} /></Field>
    {(ownerId || from || to) && <Button variant="link" className="px-1" onClick={clearScope}>{t('dcv.clearScope')}</Button>}
  </div></FilterBar>
  // The heading and scope stay in place while a page or scope loads, so a changed field keeps focus and the layout its shape.
  const shell = (content: ReactNode, evaluatedAt?: string) => <section aria-labelledby="dcv-title" className="space-y-4">
    <div className="flex flex-wrap items-start justify-between gap-x-6 gap-y-1">
      <div className="min-w-0 max-w-3xl"><h2 id="dcv-title" className="text-lg/[26px] font-semibold tracking-[-0.2px]">{t('dcv.title')}</h2><p className="mt-1 text-sm text-muted-foreground">{t('dcv.subtitle')}</p></div>
      {evaluatedAt && <span className="text-xs text-muted-foreground tabular-nums">{t('dcv.clientRefresh', { when: new Date(evaluatedAt).toLocaleTimeString() })}</span>}
    </div>
    <p role="status" className="max-w-4xl text-xs text-muted-foreground">{t('dcv.scopeNote')}</p>
    {filters}
    {(from || to) && <p role="status" className="flex gap-2 rounded-md border border-warn/40 bg-warn-bg px-4 py-3 text-sm text-warn"><span aria-hidden>▲</span><span>{t('dcv.dateScopeNote')}</span></p>}
    {content}
  </section>
  if (q.isPending) return shell(<div className="rounded-lg border bg-card"><Loading rows={2} /></div>)
  if (q.error) return shell(<ErrorBanner error={q.error} retry={() => q.refetch()} />)
  const d = q.data
  const outgoing = d.outgoing
  const incoming = d.incoming
  const scoped = !!(ownerId || disciplineId) // every count below follows the selected discipline and owner
  const wide = scoped ? '' : ` · ${t('dcv.projectWide')}`
  const registerUrl = (pathname: string, panel?: string) => {
    const params = new URLSearchParams()
    if (panel) params.set('panel', panel)
    if (ownerId && pathname !== 'handoffs') params.set('ownerId', ownerId)
    if (disciplineId && pathname !== 'changes') params.set('disciplineId', disciplineId)
    if (sp.get('discipline')) params.set('discipline', sp.get('discipline')!)
    if (from) params.set('from', from)
    if (to) params.set('to', to)
    const suffix = params.size ? `?${params}` : ''
    return `/projects/${project.projectNumber}/${pathname}${suffix}`
  }
  const none = <p className="text-muted-foreground">{t('dcv.none')}</p>
  // One of the five fixed questions: a stable identity stripe, the question as its heading, its count, and the register.
  const card = (id: string, heading: string, count: number | string, content: ReactNode, href: string) => (
    <section aria-labelledby={`dcv-${id}`} data-accent={QUESTION_ACCENT[id]} className={`flex flex-col ${box} border-t-4 border-t-(color:--acc-stripe)`}>
      <div className={head}><h2 id={`dcv-${id}`} className={title}>{heading}</h2><span className={badge}>{count}</span></div>
      <div className={`flex-1 ${body}`}>{content}</div>
      <div className="border-t px-4 py-2.5"><Link className="text-sm font-medium text-primary underline underline-offset-4" to={registerUrl(href)}>{t('dcv.openRegister')}</Link></div>
    </section>
  )
  const items = (rows: { id: string; key: string; text: string; detail?: ReactNode }[], register: string) => rows.length ? <ul className="divide-y">{rows.map((r) => <li key={r.id} className={row}>
    <p><Link className={keyLink} to={registerUrl(register, `${register === 'handoffs' ? 'Handoff' : 'ChangeNotice'}:${r.id}`)}>{r.key}</Link> <span className="font-medium">{r.text}</span></p>
    {r.detail && <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground">{r.detail}</div>}
  </li>)}</ul> : none
  const linkedIssueItems = d.linkedIssues
  const existingActions = (sourceType: LinkedAction['sourceType'], sourceId: string) => d.linkedActions.filter(a => a.sourceType === sourceType && a.sourceId === sourceId)
  const actionLinks = (rows: LinkedAction[]) => rows.length > 0 && <ul className="mt-1 space-y-1 text-xs">{rows.map(a =>
    <li key={a.id}>{t('dcv.existingAction')} <Link className={keyLink} to={registerUrl('meetings', `Action:${a.id}`)}>{a.key}</Link> · {a.text} · {tv(a.status)}{a.dueDate && <span className="tabular-nums"> · {fmtDate(a.dueDate)}</span>}</li>)}</ul>
  const capture = (key: string, onClick: () => void) => meeting && canCapture && onCapture &&
    <button type="button" className="no-print inline-flex min-h-6 items-center text-sm text-primary underline underline-offset-4" aria-label={t('dcv.reuseCaptureFor', { key })} onClick={onClick}>{t('dcv.captureAction')}</button>
  return shell(<>
    <p className="no-print text-xs text-muted-foreground">{t('dcv.allSectionsPage')}</p>
    <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
      {card('owe', t('dcv.owe'), d.outgoingTotal, <>{items(outgoing.map((h) => ({ id: h.id, key: h.key, text: h.title, detail: <><CoordStatus status={h.status} /><span>{t('dcv.promisedDate')}: <span className="tabular-nums">{h.promisedBy ? fmtDate(h.promisedBy) : t('dcv.unavailable')}</span></span><span>{t('dcv.neededDate')}: <span className="tabular-nums">{fmtDate(h.neededBy)}</span></span></> })), 'handoffs')}<Pager label={t('dcv.owe')} page={d.outgoingPage} total={d.outgoingTotal} pageSize={d.handoffPageSize} onPage={setPage} /></>, 'handoffs')}
      {card('waiting', t('dcv.waiting'), d.incomingTotal, <>{items(incoming.map((h) => ({ id: h.id, key: h.key, text: h.title, detail: <CoordStatus status={h.status} /> })), 'handoffs')}<Pager label={t('dcv.waiting')} page={d.incomingPage} total={d.incomingTotal} pageSize={d.handoffPageSize} onPage={setPage} /></>, 'handoffs')}
      {card('using', `${t('dcv.using')}${wide}`, d.usesTotal, d.uses.length ? <><ul className="divide-y">{d.uses.map(u => <li key={u.id} className={row}>
        <p><Link className={keyLink} to={registerUrl(u.targetType === 'Task' ? 'tasks' : 'deliverables', `${u.targetType}:${u.targetId}`)}>{u.targetKey ?? t('dcv.unknownWork')}</Link> <span className="font-medium">{u.targetName ?? ''}</span></p>
        <p>{t('dcv.sourceRevision')}: {u.sourceUrl ? <a className={textLink} href={u.sourceUrl} target="_blank" rel="noopener noreferrer">{u.sourceKey ?? t('dcv.unavailable')} {u.revision ? `(${u.revision})` : ''}</a> : <span className="text-muted-foreground">{t('dcv.unavailable')}</span>}</p>
        <p className="text-xs text-muted-foreground">{u.intendedUse}</p>
      </li>)}</ul><Pager label={t('dcv.using')} page={d.usesPage} total={d.usesTotal} pageSize={d.usesPageSize} onPage={setPage} /></> : none, 'changes')}
      {card('changed', `${t('dcv.changed')}${wide}`, d.changesTotal,
        d.changes.length ? <><ul className="divide-y">{d.changes.map(c => <li key={c.id} className={row}>
          <p><Link className={keyLink} to={registerUrl('changes', `ChangeNotice:${c.id}`)}>{c.key}</Link> <span className="font-medium">{c.title}</span></p>
          <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs"><CoordStatus status={c.status} /><span className={c.pendingAssessments > 0 ? 'text-warn' : 'text-muted-foreground'}>{c.pendingAssessments > 0 && <span aria-hidden>▲ </span>}<ChangeAssessmentCounts c={c} scoped={scoped} /></span></div>
          {actionLinks(existingActions('ChangeNotice', c.id))}
          {d.unavailableChangeTargets.filter(target => target.changeNoticeId === c.id).map(target =>
            <p key={target.changeNoticeId} role="status" className="text-xs text-warn"><span aria-hidden>▲ </span>{plural(target.count, 'dcv.targetUnavailableOneLinkable', 'dcv.targetUnavailableManyLinkable')}</p>)}
          {capture(c.key, () => onCapture!(c.key,
            [{ targetType: 'ChangeNotice', targetId: c.id },
              ...d.changeTargets.filter(target => target.changeNoticeId === c.id).map(target => ({ targetType: target.targetType, targetId: target.targetId }))],
            existingActions('ChangeNotice', c.id).map(a => a.id)))}
        </li>)}</ul><Pager label={t('dcv.changed')} page={d.changesPage} total={d.changesTotal} pageSize={d.changesPageSize} onPage={setPage} /></> : none, 'changes')}
      {card('start', `${t('dcv.start')}${wide}`, d.startabilityReadyTotal,
        <><p className="mb-2 text-xs text-muted-foreground">{t('dcv.readySummary', { ready: d.startabilityReadyTotal, n: d.startabilityTotal, from: d.startabilityFrom, to: d.startabilityTo })}</p>
          {d.startability.length ? <><ul className="divide-y">{d.startability.map(r => <li key={r.id} className={row}>
            <p><Link className={keyLink} to={registerUrl(r.targetType === 'Task' ? 'tasks' : 'deliverables', `${r.targetType}:${r.targetId}`)}>{r.key}</Link> <span className="font-medium">{r.name}</span></p>
            <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs"><CoordStatus status={r.state} />
              {r.blocked.length > 0 && <Flags tone="bad">{t('dcv.blocked', { list: r.blocked.join(', ') })}</Flags>}{r.unknown.length > 0 && <Flags tone="warn">{t('dcv.unknown', { list: r.unknown.join(', ') })}</Flags>}</div>
          </li>)}</ul><Pager label={t('dcv.start')} page={d.startabilityPage} total={d.startabilityTotal} pageSize={d.startabilityPageSize} onPage={setPage} /></> : <p className="text-muted-foreground">{t('dcv.noAssessedWork')}</p>}</>, 'readiness')}
    </div>
    <section aria-labelledby="dcv-linked-issues" className={box}>
      <div className={head}><h2 id="dcv-linked-issues" className={title}>{t('dcv.linkedIssues')}</h2><span className={badge}>{d.linkedIssuesTotal}</span></div>
      <div className={body}>
        <p className="mb-2 text-xs text-muted-foreground">{t('dcv.linkedIssuesHint')}</p>
        {linkedIssueItems.length ? <><ul className="divide-y rounded-md border">{linkedIssueItems.map((issue) => <li key={issue.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2">
          <Link className={keyLink} to={`/projects/${project.projectNumber}/issues?panel=Issue:${issue.id}`} aria-label={t('dcv.openLinkedIssue', { key: issue.key })}>{issue.key}</Link>
          <button type="button" className="min-w-[12rem] flex-1 text-left font-medium hover:underline" onClick={() => openPanel('Issue', issue.id)}>{issue.title}</button>
          <CoordStatus status={issue.status} />
          <span className="text-muted-foreground">{issue.ownerName ?? issue.ownerId ?? t('dcv.ownerUnavailable')}</span>
        </li>)}</ul><Pager label={t('dcv.linkedIssues')} page={d.linkedIssuesPage} total={d.linkedIssuesTotal} pageSize={d.linkedIssuesPageSize} onPage={setPage} /></> : none}
        <p className="mt-2 text-xs text-muted-foreground tabular-nums">{t('dcv.clientRefresh', { when: new Date(d.evaluatedAt).toLocaleTimeString() })}</p>
      </div>
    </section>
    <section aria-labelledby="dcv-pending-reviews" className={box}>
      <div className={head}><h2 id="dcv-pending-reviews" className={title}>{t('dcv.pendingReviews')} · {d.reviewsTotal}</h2></div>
      <div className={body}>
        {d.reviews.length ? <ul className="divide-y">{d.reviews.map(r => <li key={r.id} className={row}>
          <p><Link className={keyLink} to={registerUrl('reviews', `ReviewPackage:${r.id}`)}>{r.key}</Link> <span className="font-medium">{r.title}</span></p>
          <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground"><CoordStatus status={r.status} /><span className="tabular-nums">{t('dcv.reviewOutstanding', { n: r.outstandingDisciplines })}</span><span className={r.blockingFindings > 0 ? 'font-medium text-bad tabular-nums' : 'tabular-nums'}>{r.blockingFindings > 0 && <span aria-hidden>■ </span>}{t('dcv.reviewBlocking', { n: r.blockingFindings })}</span></div>
        </li>)}</ul> : none}
        <Pager label={t('dcv.pendingReviews')} page={d.reviewsPage} total={d.reviewsTotal} pageSize={d.reviewsPageSize} onPage={setPage} />
      </div>
    </section>
    <div className="grid gap-4 md:grid-cols-2">
      <section aria-labelledby="dcv-submissions" className={box}><div className={head}><h2 id="dcv-submissions" className={title}>{t('dcv.upcomingSubmissions')}</h2><span className={badge}>{d.upcomingSubmissionsTotal}</span></div>
        <div className={body}>{d.upcomingSubmissions.length ? <><ul className="divide-y">{d.upcomingSubmissions.map(s => <li key={s.id} className={row}>
          <p><Link className={keyLink} to={`/projects/${project.projectNumber}/submissions?panel=SubmissionPackage:${s.id}`}>{s.key}</Link> <span className="font-medium">{s.title}</span></p>
          <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground"><CoordStatus status={s.effectiveStatus} /><span className="tabular-nums">{fmtDate(s.targetDate)}</span></div>
          {s.failingChecks.length > 0 && <p className="text-xs text-warn"><span aria-hidden>▲ </span>{t('dcv.failingChecks')}: {s.failingChecks.map((c, i) => <span key={`${s.id}-${c.code}-${c.sourceId ?? 'none'}`}>{i > 0 && ', '}{c.sourcePath ? <Link className="underline underline-offset-4" to={c.sourcePath}>{c.kind}</Link> : c.kind}</span>)}</p>}
        </li>)}</ul><Pager label={t('dcv.upcomingSubmissions')} page={d.upcomingSubmissionsPage} total={d.upcomingSubmissionsTotal} pageSize={d.pageSize} onPage={setPage} /></> : none}</div>
      </section>
      <section aria-labelledby="dcv-staffing" className={box}><div className={head}><h2 id="dcv-staffing" className={title}>{t('dcv.staffingConflicts')}</h2><span className={badge}>{d.staffingConflictsTotal}</span></div>
        <div className={body}>{d.staffingConflicts.length ? <><ul className="divide-y">{d.staffingConflicts.map(c => <li key={c.id} className={row}>
          <p><Link className={keyLink} to={registerUrl(c.targetType === 'Task' ? 'tasks' : 'deliverables', `${c.targetType}:${c.targetId}`)}>{c.key}</Link> <span className="font-medium">{c.description}</span></p>
          <p className="text-xs text-muted-foreground tabular-nums">{fmtDate(c.neededBy)}</p>
        </li>)}</ul><Pager label={t('dcv.staffingConflicts')} page={d.staffingConflictsPage} total={d.staffingConflictsTotal} pageSize={d.pageSize} onPage={setPage} /></> : none}</div>
      </section>
    </div>
    {d.blockerGroups.length > 0 && <section aria-labelledby="dcv-blockers" className={box}>
      <div className={head}><h2 id="dcv-blockers" className={title}>{t('dcv.ws.linkedTaskBlockers')}{wide}</h2><span className={badge}>{d.blockerGroupsTotal}</span></div>
      <div className={body}><ul className="divide-y">{d.blockerGroups.map(group => <li key={group.handoffId} className={row}>
        <p><Link className={keyLink} to={registerUrl('handoffs', `Handoff:${group.handoffId}`)}>{group.handoffKey}</Link> · <Link className={textLink} to={`/projects/${project.projectNumber}/tasks?ids=${group.taskIds.join(',')}`}>{t('dcv.linkedTasks', { n: group.taskIds.length })}</Link> ({group.taskIds.map((id, i) => <span key={id}>{i > 0 && ', '}<Link className={keyLink} to={`/projects/${project.projectNumber}/tasks?panel=Task:${id}`}>{group.taskKeys[i] ?? id}</Link></span>)})</p>
        {actionLinks(existingActions('Handoff', group.handoffId))}
        {capture(group.handoffKey, () => onCapture!(group.handoffKey,
          [{ targetType: 'Handoff', targetId: group.handoffId }, ...group.taskIds.map(id => ({ targetType: 'Task', targetId: id }))],
          existingActions('Handoff', group.handoffId).map(a => a.id)))}
      </li>)}</ul><Pager label={t('dcv.ws.linkedTaskBlockers')} page={d.blockerGroupsPage} total={d.blockerGroupsTotal} pageSize={d.pageSize} onPage={setPage} /></div>
    </section>}
  </>, d.evaluatedAt)
}
