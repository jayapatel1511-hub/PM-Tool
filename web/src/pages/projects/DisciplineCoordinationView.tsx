import { useQuery } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router'
import { useEffect, type ReactNode } from 'react'
import { ErrorBanner, Loading } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { useItemPanel } from '@/components/hub/panel-host'
import { get, qs } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { plural, t, tv } from '@/lib/i18n'
import type { ProjectDetail } from '@/lib/types'

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

function Pager({ label, page, total, pageSize, onPage }: { label: string; page: number; total: number; pageSize: number; onPage: (page: number) => void }) {
  if (pageSize <= 0 || total <= pageSize) return null
  return <div className="no-print mt-2 flex items-center justify-end gap-2" aria-label={label}>
    <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => onPage(page - 1)}>{t('handoff.previous')}</Button>
    <span className="text-xs" aria-live="polite">{t('handoff.page', { n: page })} · {total}</span>
    <Button size="sm" variant="outline" disabled={page * pageSize >= total} onClick={() => onPage(page + 1)}>{t('handoff.next')}</Button>
  </div>
}

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
    queryFn: () => get<Data>(`projects/${project.id}/discipline-coordination${qs({ disciplineId, ownerId, from, to, page: printAll ? undefined : page, pageSize: printAll ? undefined : 4 })}`),
  })
  useEffect(() => {
    if (!printAll || q.isPending || !onPrinted) return
    if (q.error) { onPrinted(false); return }
    const frame = requestAnimationFrame(() => { try { window.print() } finally { onPrinted(false) } })
    return () => cancelAnimationFrame(frame)
  }, [printAll, q.isPending, q.error, onPrinted])
  const filters = <div role="group" className="mb-3 flex flex-wrap items-end gap-3 rounded border bg-background/60 p-3" aria-label={t('dcv.scopeLabel')}>
    <span className="self-center text-xs text-muted-foreground">{t('dcv.projectPrefix')} <strong>{project.projectNumber}</strong>{disciplineId ? ` · ${project.disciplines.find(x => x.id === disciplineId)?.name ?? t('dcv.selectedDiscipline')}` : ''}</span>
    <label className="text-xs">{t('common.owner')}<select className="mt-1 block rounded border bg-background px-2 py-1 text-sm" value={ownerId} onChange={e => setScope('owner', e.target.value)}><option value="">{t('dcv.allPermittedOwners')}</option>{team.data?.members.map(m => <option key={m.userId} value={m.userId}>{m.displayName}</option>)}</select></label>
    <label className="text-xs">{t('common.from')}<input className="mt-1 block rounded border bg-background px-2 py-1 text-sm" type="date" value={from} onChange={e => setScope('from', e.target.value)} /></label>
    <label className="text-xs">{t('common.to')}<input className="mt-1 block rounded border bg-background px-2 py-1 text-sm" type="date" value={to} onChange={e => setScope('to', e.target.value)} /></label>
    {(ownerId || from || to) && <button type="button" className="px-2 py-1 text-xs text-primary underline" onClick={clearScope}>{t('dcv.clearScope')}</button>}
  </div>
  if (q.isPending) return <Loading rows={2} />
  if (q.error) return <section aria-label={t('dcv.scopeLabel')}>{filters}<ErrorBanner error={q.error} retry={() => q.refetch()} /></section>
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
  const link = (path: string, label: string) => <Link className="text-xs font-medium text-primary underline" to={registerUrl(path)}>{label}</Link>
  const card = (id: string, title: string, count: number | string, content: ReactNode, href: string) => (
    <section aria-labelledby={`dcv-${id}`} className="rounded-md border bg-card p-3">
      <div className="flex items-center justify-between gap-2"><h2 id={`dcv-${id}`} className="font-medium">{title}</h2><span className="rounded-full bg-muted px-2 text-sm tabular-nums">{count}</span></div>
      <div className="mt-2 text-sm">{content}</div><div className="mt-2">{link(href, t('dcv.openRegister'))}</div>
    </section>
  )
  const items = (rows: { id: string; key: string; text: string; detail?: string }[], register: string) => rows.length ? <ul className="space-y-1">{rows.map((r) => <li key={r.id}><Link className="underline" to={registerUrl(register, `${register === 'handoffs' ? 'Handoff' : 'ChangeNotice'}:${r.id}`)}>{r.key}</Link> <span>{r.text}</span>{r.detail && <span className="text-muted-foreground"> · {r.detail}</span>}</li>)}</ul> : <p className="text-muted-foreground">{t('dcv.none')}</p>
  const linkedIssueItems = d.linkedIssues
  const existingActions = (sourceType: LinkedAction['sourceType'], sourceId: string) => d.linkedActions.filter(a => a.sourceType === sourceType && a.sourceId === sourceId)
  const actionLinks = (rows: LinkedAction[]) => rows.length > 0 && <ul className="mt-1 space-y-1">{rows.map(a =>
    <li key={a.id}>{t('dcv.existingAction')} <Link className="text-primary underline" to={registerUrl('meetings', `Action:${a.id}`)}>{a.key}</Link> · {a.text} · {tv(a.status)}{a.dueDate && ` · ${fmtDate(a.dueDate)}`}</li>)}</ul>
  return <section aria-labelledby="dcv-title" className="rounded-lg border border-primary/20 bg-primary/5 p-4">
    <div className="mb-3 flex flex-wrap items-baseline justify-between gap-2"><div><h2 id="dcv-title" className="text-lg font-semibold">{t('dcv.title')}</h2><p className="text-sm text-muted-foreground">{t('dcv.subtitle')}</p><p role="status" className="mt-1 text-xs text-muted-foreground">{t('dcv.scopeNote')}</p></div><span className="text-xs text-muted-foreground">{t('dcv.clientRefresh', { when: new Date(d.evaluatedAt).toLocaleTimeString() })}</span></div>
    {filters}
    <p className="no-print my-2 text-xs text-muted-foreground">{t('dcv.allSectionsPage')}</p>
    {(from || to) && <p role="status" className="mb-3 rounded border border-warn/30 bg-warn-bg px-3 py-2 text-xs text-warn">{t('dcv.dateScopeNote')}</p>}
    <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
      {card('owe', t('dcv.owe'), d.outgoingTotal, <><>{items(outgoing.map((h) => ({ id: h.id, key: h.key, text: h.title, detail: `${tv(h.status)} · ${t('dcv.promisedDate')}: ${h.promisedBy ? fmtDate(h.promisedBy) : t('dcv.unavailable')} · ${t('dcv.neededDate')}: ${fmtDate(h.neededBy)}` })), 'handoffs')}</><Pager label={t('dcv.owe')} page={d.outgoingPage} total={d.outgoingTotal} pageSize={d.handoffPageSize} onPage={setPage} /></>, 'handoffs')}
      {card('waiting', t('dcv.waiting'), d.incomingTotal, <><>{items(incoming.map((h) => ({ id: h.id, key: h.key, text: h.title, detail: tv(h.status) })), 'handoffs')}</><Pager label={t('dcv.waiting')} page={d.incomingPage} total={d.incomingTotal} pageSize={d.handoffPageSize} onPage={setPage} /></>, 'handoffs')}
      {card('using', `${t('dcv.using')}${wide}`, d.usesTotal, d.uses.length ? <><ul className="space-y-1">{d.uses.map(u => <li key={u.id}><Link className="font-medium underline" to={registerUrl(u.targetType === 'Task' ? 'tasks' : 'deliverables', `${u.targetType}:${u.targetId}`)}>{u.targetKey ?? t('dcv.unknownWork')}</Link> · {u.targetName ?? ''} · {t('dcv.sourceRevision')}: {u.sourceUrl ? <a className="underline" href={u.sourceUrl} target="_blank" rel="noopener noreferrer">{u.sourceKey ?? t('dcv.unavailable')} {u.revision ? `(${u.revision})` : ''}</a> : t('dcv.unavailable')}<span className="block text-xs text-muted-foreground">{u.intendedUse}</span></li>)}</ul><Pager label={t('dcv.using')} page={d.usesPage} total={d.usesTotal} pageSize={d.usesPageSize} onPage={setPage} /></> : <p className="text-muted-foreground">{t('dcv.none')}</p>, 'changes')}
      {card('changed', `${t('dcv.changed')}${wide}`, d.changesTotal,
        d.changes.length ? <><ul className="space-y-2">{d.changes.map(c => <li key={c.id}>
          <Link className="underline" to={registerUrl('changes', `ChangeNotice:${c.id}`)}>{c.key}</Link> · {c.title} · {tv(c.status)} · <ChangeAssessmentCounts c={c} scoped={scoped} />
          {actionLinks(existingActions('ChangeNotice', c.id))}
          {d.unavailableChangeTargets.filter(target => target.changeNoticeId === c.id).map(target =>
            <p key={target.changeNoticeId} role="status">{plural(target.count, 'dcv.targetUnavailableOneLinkable', 'dcv.targetUnavailableManyLinkable')}</p>)}
          {meeting && canCapture && onCapture &&
            <button type="button" className="no-print text-primary underline" aria-label={t('dcv.reuseCaptureFor', { key: c.key })} onClick={() => onCapture(c.key,
              [{ targetType: 'ChangeNotice', targetId: c.id },
                ...d.changeTargets.filter(target => target.changeNoticeId === c.id).map(target => ({ targetType: target.targetType, targetId: target.targetId }))],
              existingActions('ChangeNotice', c.id).map(a => a.id))}>
              {t('dcv.captureAction')}</button>}
        </li>)}</ul><Pager label={t('dcv.changed')} page={d.changesPage} total={d.changesTotal} pageSize={d.changesPageSize} onPage={setPage} /></> : <p className="text-muted-foreground">{t('dcv.none')}</p>, 'changes')}
      {card('start', `${t('dcv.start')}${wide}`, d.startabilityReadyTotal,
        <><p className="text-xs text-muted-foreground">{t('dcv.readySummary', { ready: d.startabilityReadyTotal, n: d.startabilityTotal, from: d.startabilityFrom, to: d.startabilityTo })}</p>
          {d.startability.length ? <><ul className="mt-1 space-y-1">{d.startability.map(r => <li key={r.id}>
            <Link className="underline" to={registerUrl(r.targetType === 'Task' ? 'tasks' : 'deliverables', `${r.targetType}:${r.targetId}`)}>{r.key}</Link> · {r.name} · {tv(r.state)}
            {r.blocked.length > 0 && <span> · {t('dcv.blocked', { list: r.blocked.join(', ') })}</span>}{r.unknown.length > 0 && <span> · {t('dcv.unknown', { list: r.unknown.join(', ') })}</span>}
          </li>)}</ul><Pager label={t('dcv.start')} page={d.startabilityPage} total={d.startabilityTotal} pageSize={d.startabilityPageSize} onPage={setPage} /></> : <p>{t('dcv.noAssessedWork')}</p>}</>, 'readiness')}
    </div>
    <section aria-labelledby="dcv-linked-issues" className="mt-3 rounded-md border bg-card p-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 id="dcv-linked-issues" className="font-medium">{t('dcv.linkedIssues')}</h2>
        <span className="rounded-full bg-muted px-2 text-sm tabular-nums">{d.linkedIssuesTotal}</span>
      </div>
      <p className="mt-1 text-xs text-muted-foreground">{t('dcv.linkedIssuesHint')}</p>
      {linkedIssueItems.length ? <><ul className="mt-2 divide-y rounded border">{linkedIssueItems.map((issue) => <li key={issue.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2 text-sm">
        <Link className="font-medium text-primary underline" to={`/projects/${project.projectNumber}/issues?panel=Issue:${issue.id}`} aria-label={t('dcv.openLinkedIssue', { key: issue.key })}>{issue.key}</Link>
        <button type="button" className="min-w-[12rem] flex-1 text-left hover:underline" onClick={() => openPanel('Issue', issue.id)}>{issue.title}</button>
        <span className="text-muted-foreground">{tv(issue.status)}</span>
        <span className="text-muted-foreground">{issue.ownerName ?? issue.ownerId ?? t('dcv.ownerUnavailable')}</span>
      </li>)}</ul><Pager label={t('dcv.linkedIssues')} page={d.linkedIssuesPage} total={d.linkedIssuesTotal} pageSize={d.linkedIssuesPageSize} onPage={setPage} /></> : <p className="mt-2 text-sm text-muted-foreground">{t('dcv.none')}</p>}
      <p className="mt-2 text-xs text-muted-foreground">{t('dcv.clientRefresh', { when: new Date(d.evaluatedAt).toLocaleTimeString() })}</p>
    </section>
    <section aria-labelledby="dcv-pending-reviews" className="mt-3 rounded border bg-card p-3">
      <h2 id="dcv-pending-reviews" className="font-medium">{t('dcv.pendingReviews')} · {d.reviewsTotal}</h2>
      {d.reviews.length ? <ul className="mt-2 space-y-1">{d.reviews.map(r => <li key={r.id}>
        <Link className="underline" to={registerUrl('reviews', `ReviewPackage:${r.id}`)}>{r.key}</Link> · {r.title} · {tv(r.status)} · {t('dcv.reviewOutstanding', { n: r.outstandingDisciplines })} · {t('dcv.reviewBlocking', { n: r.blockingFindings })}
      </li>)}</ul> : <p>{t('dcv.none')}</p>}
      <Pager label={t('dcv.pendingReviews')} page={d.reviewsPage} total={d.reviewsTotal} pageSize={d.reviewsPageSize} onPage={setPage} />
    </section>
    <div className="mt-3 grid gap-3 md:grid-cols-2">
      <section aria-labelledby="dcv-submissions" className="rounded-md border bg-card p-3"><div className="flex items-center justify-between"><h2 id="dcv-submissions" className="font-medium">{t('dcv.upcomingSubmissions')}</h2><span className="rounded-full bg-muted px-2 text-sm tabular-nums">{d.upcomingSubmissionsTotal}</span></div>
        {d.upcomingSubmissions.length ? <><ul className="mt-2 space-y-2 text-sm">{d.upcomingSubmissions.map(s => <li key={s.id}><Link className="underline" to={`/projects/${project.projectNumber}/submissions?panel=SubmissionPackage:${s.id}`}>{s.key}</Link> · {s.title} · {tv(s.effectiveStatus)} · {fmtDate(s.targetDate)}{s.failingChecks.length > 0 && <span className="block text-warn">{t('dcv.failingChecks')}: {s.failingChecks.map(c => c.sourcePath ? <Link key={`${s.id}-${c.code}-${c.sourceId ?? 'none'}`} className="underline" to={c.sourcePath}>{c.kind}</Link> : c.kind)}</span>}</li>)}</ul><Pager label={t('dcv.upcomingSubmissions')} page={d.upcomingSubmissionsPage} total={d.upcomingSubmissionsTotal} pageSize={d.pageSize} onPage={setPage} /></> : <p className="mt-2 text-sm text-muted-foreground">{t('dcv.none')}</p>}
      </section>
      <section aria-labelledby="dcv-staffing" className="rounded-md border bg-card p-3"><div className="flex items-center justify-between"><h2 id="dcv-staffing" className="font-medium">{t('dcv.staffingConflicts')}</h2><span className="rounded-full bg-muted px-2 text-sm tabular-nums">{d.staffingConflictsTotal}</span></div>
        {d.staffingConflicts.length ? <><ul className="mt-2 space-y-2 text-sm">{d.staffingConflicts.map(c => <li key={c.id}><Link className="underline" to={registerUrl(c.targetType === 'Task' ? 'tasks' : 'deliverables', `${c.targetType}:${c.targetId}`)}>{c.key}</Link> · {c.description} · {fmtDate(c.neededBy)}</li>)}</ul><Pager label={t('dcv.staffingConflicts')} page={d.staffingConflictsPage} total={d.staffingConflictsTotal} pageSize={d.pageSize} onPage={setPage} /></> : <p className="mt-2 text-sm text-muted-foreground">{t('dcv.none')}</p>}
      </section>
    </div>
    {d.blockerGroups.length > 0 && <section aria-labelledby="dcv-blockers" className="mt-3 rounded-md border bg-card p-3">
      <h2 id="dcv-blockers" className="font-medium">{t('dcv.ws.linkedTaskBlockers')}{wide}</h2>
      <ul className="mt-2 space-y-2 text-sm">{d.blockerGroups.map(group => <li key={group.handoffId}><Link className="font-medium text-primary underline" to={registerUrl('handoffs', `Handoff:${group.handoffId}`)}>{group.handoffKey}</Link> · <Link className="text-primary underline" to={`/projects/${project.projectNumber}/tasks?ids=${group.taskIds.join(',')}`}>{t('dcv.linkedTasks', { n: group.taskIds.length })}</Link> ({group.taskIds.map((id, i) => <span key={id}>{i > 0 && ', '}<Link className="text-primary underline" to={`/projects/${project.projectNumber}/tasks?panel=Task:${id}`}>{group.taskKeys[i] ?? id}</Link></span>)})
        {actionLinks(existingActions('Handoff', group.handoffId))}
        {meeting && canCapture && onCapture && <button type="button" className="no-print text-primary underline" aria-label={t('dcv.reuseCaptureFor', { key: group.handoffKey })} onClick={() => onCapture(group.handoffKey,
          [{ targetType: 'Handoff', targetId: group.handoffId }, ...group.taskIds.map(id => ({ targetType: 'Task', targetId: id }))],
          existingActions('Handoff', group.handoffId).map(a => a.id))}>
          {t('dcv.captureAction')}</button>}
      </li>)}</ul><Pager label={t('dcv.ws.linkedTaskBlockers')} page={d.blockerGroupsPage} total={d.blockerGroupsTotal} pageSize={d.pageSize} onPage={setPage} />
    </section>}
  </section>
}
