import { useQuery } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router'
import type { ReactNode } from 'react'
import { ErrorBanner, Loading } from '@/components/hub/common'
import { useItemPanel } from '@/components/hub/panel-host'
import { useMe } from '@/lib/auth'
import { get, qs } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import type { ProjectDetail } from '@/lib/types'

type Handoff = { id: string; key: string; title: string; status: string; neededBy: string; promisedBy?: string; targetKey?: string; sendingOwnerId: string; receivingOwnerId: string; sendingDisciplineId: string; receivingDisciplineId: string }
type Change = { id: string; key: string; title: string; status: string; pendingAssessments: number; ownerId?: string }
type Review = { id: string; key: string; title: string; status: string; outstandingDisciplines: number; blockingFindings: number; coordinatorId?: string }
type LinkedIssue = { id: string; key: string; title: string; status: string; ownerId?: string | null; ownerName?: string | null; projectDisciplineId?: string | null }
type InputUse = { id: string; targetType: string; targetId: string; sourceRevisionId: string }
type BlockerGroup = { handoffId: string; handoffKey: string; taskIds: string[]; taskKeys: string[] }
type Startability = { id: string; targetType: 'Task' | 'Deliverable'; targetId: string; key: string; name: string;
  dueDate: string | null; state: string; blocked: string[]; unknown: string[] }
type LinkedAction = { id: string; key: string; text: string; status: string; dueDate: string | null;
  sourceType: 'Handoff' | 'ChangeNotice'; sourceId: string; targetType: 'Task' | 'Deliverable'; targetId: string }
type ChangeTarget = { changeNoticeId: string; targetType: 'Task' | 'Deliverable'; targetId: string }
type UnavailableChangeTarget = { changeNoticeId: string; count: number }
type Data = { handoffs: Handoff[]; changes: Change[]; reviews: Review[]; linkedIssues: LinkedIssue[]; uses: InputUse[]; blockerGroups: BlockerGroup[]; usesTotal: number; linkedIssuesTotal: number; handoffsTotal: number; changesTotal: number; reviewsTotal: number; evaluatedAt: string;
  startability: Startability[]; startabilityReadyTotal: number; startabilityFrom: string; startabilityTo: string;
  linkedActions: LinkedAction[]; changeTargets: ChangeTarget[]; unavailableChangeTargets: UnavailableChangeTarget[] }

/** Packet 030's five-question coordination projection over the existing registers. */
export function DisciplineCoordinationView({ project, disciplineId, meeting, canCapture, onCapture }: {
  project: ProjectDetail; disciplineId?: string; meeting?: boolean; canCapture?: boolean;
  onCapture?: (label: string, links: { targetType: string; targetId: string }[]) => void
}) {
  const me = useMe()
  const openPanel = useItemPanel()
  const [sp, setSp] = useSearchParams()
  const ownerId = sp.get('owner') ?? ''
  const from = sp.get('from') ?? ''
  const to = sp.get('to') ?? ''
  const setScope = (name: string, value: string) => setSp(p => {
    const next = new URLSearchParams(p)
    if (value) next.set(name, value); else next.delete(name)
    return next
  }, { replace: true })
  const clearScope = () => setSp(p => { const next = new URLSearchParams(p); ['owner', 'from', 'to'].forEach(name => next.delete(name)); return next }, { replace: true })
  const team = useQuery({ queryKey: ['p', project.id, 'team'], queryFn: () => get<{ members: { userId: string; displayName: string }[] }>(`projects/${project.id}/team`), staleTime: 60_000 })
  const q = useQuery({
    queryKey: ['p', project.id, 'discipline-coordination', disciplineId, ownerId, from, to],
    queryFn: () => get<Data>(`projects/${project.id}/discipline-coordination${qs({ disciplineId, ownerId, from, to })}`),
  })
  const filters = <div className="mb-3 flex flex-wrap items-end gap-3 rounded border bg-background/60 p-3" aria-label="Coordination scope">
    <span className="self-center text-xs text-muted-foreground">Project: <strong>{project.projectNumber}</strong>{disciplineId ? ` · ${project.disciplines.find(x => x.id === disciplineId)?.name ?? 'Selected discipline'}` : ''}</span>
    <label className="text-xs">Owner<select className="mt-1 block rounded border bg-background px-2 py-1 text-sm" value={ownerId} onChange={e => setScope('owner', e.target.value)}><option value="">All permitted owners</option>{team.data?.members.map(m => <option key={m.userId} value={m.userId}>{m.displayName}</option>)}</select></label>
    <label className="text-xs">From<input className="mt-1 block rounded border bg-background px-2 py-1 text-sm" type="date" value={from} onChange={e => setScope('from', e.target.value)} /></label>
    <label className="text-xs">To<input className="mt-1 block rounded border bg-background px-2 py-1 text-sm" type="date" value={to} onChange={e => setScope('to', e.target.value)} /></label>
    {(ownerId || from || to) && <button type="button" className="px-2 py-1 text-xs text-primary underline" onClick={clearScope}>Clear scope</button>}
  </div>
  if (q.isPending) return <Loading rows={2} />
  if (q.error) return <section aria-label="Coordination scope">{filters}<ErrorBanner error={q.error} retry={() => q.refetch()} /></section>
  const d = q.data
  const scopedOwnerId = ownerId || me.id
  const inDateScope = (h: Handoff) => (!from || (h.promisedBy ?? h.neededBy) >= from) && (!to || (h.promisedBy ?? h.neededBy) <= to)
  const outgoing = d.handoffs.filter((h) => h.status !== 'Cancelled' && h.status !== 'Incorporated' && (h.promisedBy || h.status === 'Draft') &&
    (disciplineId ? h.sendingDisciplineId === disciplineId : h.sendingOwnerId === scopedOwnerId) && (!ownerId || h.sendingOwnerId === ownerId) && inDateScope(h))
  const incoming = d.handoffs.filter((h) => ['Submitted', 'Clarification Requested', 'Returned', 'Accepted'].includes(h.status) &&
    (disciplineId ? h.receivingDisciplineId === disciplineId : h.receivingOwnerId === scopedOwnerId) && (!ownerId || h.receivingOwnerId === ownerId) && inDateScope(h))
  const openChanges = d.changes.filter((c) => c.status === 'Open' || c.pendingAssessments > 0)
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
  const items = (rows: { id: string; key: string; text: string; detail?: string }[], register: string) => rows.length ? <ul className="space-y-1">{rows.slice(0, 4).map((r) => <li key={r.id}><Link className="underline" to={registerUrl(register, `${register === 'handoffs' ? 'Handoff' : 'ChangeNotice'}:${r.id}`)}>{r.key}</Link> <span>{r.text}</span>{r.detail && <span className="text-muted-foreground"> · {r.detail}</span>}</li>)}</ul> : <p className="text-muted-foreground">{t('dcv.none')}</p>
  const linkedIssueItems = d.linkedIssues.slice(0, 4)
  const existingActions = (sourceType: LinkedAction['sourceType'], sourceId: string) => d.linkedActions.filter(a => a.sourceType === sourceType && a.sourceId === sourceId)
  const actionLinks = (rows: LinkedAction[]) => rows.length > 0 && <ul className="mt-1 space-y-1">{rows.map(a =>
    <li key={a.id}>Existing action: <Link className="text-primary underline" to={registerUrl('meetings', `Action:${a.id}`)}>{a.key}</Link> · {a.text} · {tv(a.status)}{a.dueDate && ` · ${fmtDate(a.dueDate)}`}</li>)}</ul>
  return <section aria-labelledby="dcv-title" className="rounded-lg border border-primary/20 bg-primary/5 p-4">
    <div className="mb-3 flex flex-wrap items-baseline justify-between gap-2"><div><h2 id="dcv-title" className="text-lg font-semibold">{t('dcv.title')}</h2><p className="text-sm text-muted-foreground">{t('dcv.subtitle')}</p><p role="status" className="mt-1 text-xs text-muted-foreground">{t('dcv.scopeNote')}</p></div><span className="text-xs text-muted-foreground">{t('dcv.clientRefresh', { when: new Date(d.evaluatedAt).toLocaleTimeString() })}</span></div>
    {filters}
    {(from || to) && <p role="status" className="mb-3 rounded border border-warn/30 bg-warn-bg px-3 py-2 text-xs text-warn">Date scope filters handoff due/promised dates and assessed work due dates. Changes, reviews, and input uses do not expose compatible date fields.</p>}
    <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
      {card('owe', t('dcv.owe'), outgoing.length, items(outgoing.map((h) => ({ id: h.id, key: h.key, text: h.title, detail: `${tv(h.status)} · ${fmtDate(h.promisedBy ?? h.neededBy)}` })), 'handoffs'), 'handoffs')}
      {card('waiting', t('dcv.waiting'), incoming.length, items(incoming.map((h) => ({ id: h.id, key: h.key, text: h.title, detail: tv(h.status) })), 'handoffs'), 'handoffs')}
      {card('using', `${t('dcv.using')} · ${t('dcv.projectWide')}`, d.usesTotal, d.uses.length ? <p>{t('dcv.usingHint', { n: d.usesTotal })}</p> : <p className="text-muted-foreground">{t('dcv.none')}</p>, 'changes')}
      {card('changed', `${t('dcv.changed')}${ownerId ? '' : ` · ${t('dcv.projectWide')}`}`, openChanges.length,
        openChanges.length ? <ul className="space-y-2">{openChanges.map(c => <li key={c.id}>
          <Link className="underline" to={registerUrl('changes', `ChangeNotice:${c.id}`)}>{c.key}</Link> · {c.title} · {tv(c.status)} · {c.pendingAssessments} {t('dcv.assessments')}
          {actionLinks(existingActions('ChangeNotice', c.id))}
          {d.unavailableChangeTargets.filter(target => target.changeNoticeId === c.id).map(target =>
            <p key={target.changeNoticeId} role="status">{target.count} assessment target{target.count === 1 ? '' : 's'} unavailable; the action can still link to this change.</p>)}
          {meeting && canCapture && onCapture &&
            <button type="button" className="no-print text-primary underline" onClick={() => onCapture(c.key,
              [{ targetType: 'ChangeNotice', targetId: c.id },
                ...d.changeTargets.filter(target => target.changeNoticeId === c.id).map(target => ({ targetType: target.targetType, targetId: target.targetId }))])}>
              {existingActions('ChangeNotice', c.id).length ? 'Create separate action' : 'Capture action'}</button>}
        </li>)}</ul> : <p className="text-muted-foreground">{t('dcv.none')}</p>, 'changes')}
      {card('start', `${t('dcv.start')}${ownerId || disciplineId ? '' : ` · ${t('dcv.projectWide')}`}`, d.startabilityReadyTotal,
        <><p className="text-xs text-muted-foreground">{d.startabilityReadyTotal} Ready of {d.startability.length} assessed · due {d.startabilityFrom} to {d.startabilityTo}</p>
          {d.startability.length ? <ul className="mt-1 space-y-1">{d.startability.slice(0, 4).map(r => <li key={r.id}>
            <Link className="underline" to={registerUrl(r.targetType === 'Task' ? 'tasks' : 'deliverables', `${r.targetType}:${r.targetId}`)}>{r.key}</Link> · {r.name} · {tv(r.state)}
            {r.blocked.length > 0 && <span> · Blocked: {r.blocked.join(', ')}</span>}{r.unknown.length > 0 && <span> · Unknown: {r.unknown.join(', ')}</span>}
          </li>)}</ul> : <p>No assessed work is due in this window.</p>}
          {d.startability.length > 4 && <p>Showing 4 of {d.startability.length} assessed items. Open readiness for more.</p>}</>, 'readiness')}
    </div>
    <section aria-labelledby="dcv-linked-issues" className="mt-3 rounded-md border bg-card p-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 id="dcv-linked-issues" className="font-medium">{t('dcv.linkedIssues')}</h2>
        <span className="rounded-full bg-muted px-2 text-sm tabular-nums">{d.linkedIssuesTotal}</span>
      </div>
      <p className="mt-1 text-xs text-muted-foreground">{t('dcv.linkedIssuesHint')}</p>
      {linkedIssueItems.length ? <ul className="mt-2 divide-y rounded border">{linkedIssueItems.map((issue) => <li key={issue.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2 text-sm">
        <Link className="font-medium text-primary underline" to={`/projects/${project.projectNumber}/issues?panel=Issue:${issue.id}`} aria-label={t('dcv.openLinkedIssue', { key: issue.key })}>{issue.key}</Link>
        <button type="button" className="min-w-[12rem] flex-1 text-left hover:underline" onClick={() => openPanel('Issue', issue.id)}>{issue.title}</button>
        <span className="text-muted-foreground">{tv(issue.status)}</span>
        <span className="text-muted-foreground">{issue.ownerName ?? issue.ownerId ?? t('dcv.ownerUnavailable')}</span>
      </li>)}</ul> : <p className="mt-2 text-sm text-muted-foreground">{t('dcv.none')}</p>}
      <p className="mt-2 text-xs text-muted-foreground">{t('dcv.clientRefresh', { when: new Date(d.evaluatedAt).toLocaleTimeString() })}</p>
    </section>
    {d.blockerGroups.length > 0 && <section aria-labelledby="dcv-blockers" className="mt-3 rounded-md border bg-card p-3">
      <h2 id="dcv-blockers" className="font-medium">{t('dcv.waiting')}</h2>
      <ul className="mt-2 space-y-2 text-sm">{d.blockerGroups.map(group => <li key={group.handoffId}><Link className="font-medium text-primary underline" to={registerUrl('handoffs', `Handoff:${group.handoffId}`)}>{group.handoffKey}</Link> · {group.taskKeys.length} linked tasks ({group.taskIds.map((id, i) => <span key={id}>{i > 0 && ', '}<Link className="text-primary underline" to={`/projects/${project.projectNumber}/tasks?panel=Task:${id}`}>{group.taskKeys[i] ?? id}</Link></span>)})
        {actionLinks(existingActions('Handoff', group.handoffId))}
        {meeting && canCapture && onCapture && <button type="button" className="no-print text-primary underline" onClick={() => onCapture(group.handoffKey,
          [{ targetType: 'Handoff', targetId: group.handoffId }, ...group.taskIds.map(id => ({ targetType: 'Task', targetId: id }))])}>
          {existingActions('Handoff', group.handoffId).length ? 'Create separate action' : 'Capture action'}</button>}
      </li>)}</ul>
    </section>}
  </section>
}
