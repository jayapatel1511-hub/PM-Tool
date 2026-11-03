import { useQuery } from '@tanstack/react-query'
import type { MouseEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router'
import { Empty, ErrorBanner, Loading, Missing } from '@/components/hub/common'
import { Key, StatusPill } from '@/components/hub/pills'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { typedRequest } from '@/lib/api'
import { useMe } from '@/lib/auth'
import { fmtDate, fmtTime } from '@/lib/format'
import { plural, t } from '@/lib/i18n'
import type { components } from '@/gen/api'
import { captureRecordContextOrigin } from '@/lib/record-context-navigation'

type ProjectRow = components['schemas']['ProjectRow']

export function ProjectPlainProgress({ row }: { row: Pick<ProjectRow, 'progressFacts'> }) {
  const facts = row.progressFacts
  return <div className="space-y-1 text-xs tabular-nums" data-project-progress-facts>
    <p>{facts?.elapsedPct == null ? t('projectScreens.elapsedUnknown') : t('projectScreens.elapsed', { n: facts.elapsedPct })}</p>
    <p>{facts?.progressPct == null ? t('projectScreens.progressUnknown') : t('projectScreens.progress', { n: facts.progressPct })}</p>
  </div>
}

export function ProjectStaffing({ row }: { row: ProjectRow }) {
  const me = useMe()
  const staffing = row.staffing
  if (!staffing) return <span className="text-xs text-muted-foreground">{t('common.unavailable')}</span>
  const planner = new URLSearchParams({ teamProjectId: row.id, from: staffing.from, weeks: String(staffing.lookaheadWeeks), indicator: 'OverPlanned' })
  if (staffing.ownPlannerOnly) planner.set('personId', me.id)
  return <div className="min-w-44 space-y-1 text-xs" data-project-staffing={row.id}>
    <div className="flex flex-wrap items-center gap-x-1.5 gap-y-1">
      <Link className="underline underline-offset-2" to={`/projects/${encodeURIComponent(row.projectNumber)}/allocations?tab=roles&roleStatus=Open`} onClick={e => e.stopPropagation()}>{plural(staffing.openRoles, 'projectScreens.openRole', 'projectScreens.openRoles')}</Link>
      <span aria-hidden>·</span>
      {staffing.overCapacityPeople == null ? <span>{t('projectScreens.capacityUnavailable')}</span> : <Link className="underline underline-offset-2" to={`/planner?${planner}`} onClick={e => e.stopPropagation()}>{t(staffing.partialView ? 'projectScreens.shownOverCapacity' : 'projectScreens.overCapacity', { n: staffing.overCapacityPeople })}</Link>}
    </div>
    <p className="text-muted-foreground">{t('projectScreens.staffingWindow', { n: staffing.lookaheadWeeks, from: fmtDate(staffing.from), through: fmtDate(staffing.through) })}</p>
    {staffing.limitedScope && <p className="text-muted-foreground">{t('projectScreens.staffingScope')}</p>}
    {staffing.partialView && <p className="text-muted-foreground">{t('projectScreens.staffingPartial')}</p>}
  </div>
}

const day = (value: string) => Date.parse(`${value}T00:00:00Z`) / 86400000
export function ProjectSpan({ row }: { row: ProjectRow }) {
  const span = row.span
  if (!span) return <span className="text-xs text-muted-foreground">{t('common.unavailable')}</span>
  const start = row.startDate, target = row.targetCompletionDate
  const valid = start && target && day(target) > day(start)
  const marker = valid ? Math.max(0, Math.min(100, (day(span.today) - day(start)) / (day(target) - day(start)) * 100)) : null
  return <div className="min-w-36 max-w-56 space-y-1.5 text-xs" data-project-span={row.id}>
    {marker != null ? <><div className="relative h-1 rounded bg-muted" role="img" aria-label={t('projectScreens.spanDescription', { from: fmtDate(start), through: fmtDate(target), today: fmtDate(span.today) })}>
      <span className="absolute -top-1 h-3 w-px bg-foreground" style={{ left: `${marker}%` }} aria-hidden />
    </div><p className="text-muted-foreground tabular-nums">{fmtDate(start)} → {fmtDate(target)}</p></> : <p className="text-muted-foreground">{!target ? t('projectScreens.noTarget') : !start ? t('projectScreens.noStart') : t('projectScreens.spanUnavailable')}</p>}
    <p>{plural(span.openMilestones, 'projectScreens.milestoneLeft', 'projectScreens.milestonesLeft')}</p>
  </div>
}

/** Mounting the peek triggers its own authorized read; no list-row snapshot stands in for details. */
export function ProjectPeek({ id, close }: { id: string; close: () => void }) {
  const location = useLocation(), navigate = useNavigate()
  // Peek belongs to project_screens; restoring its list must not depend on the
  // separate screen_patterns switch. Native modified clicks keep their href.
  const followMilestone = (event: MouseEvent<HTMLAnchorElement>) => {
    if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return
    if (event.currentTarget.target && event.currentTarget.target !== '_self') return
    const target = new URL(event.currentTarget.href)
    if (target.origin !== window.location.origin) return
    event.preventDefault()
    navigate(`${target.pathname}${target.search}${target.hash}`, { state: { recordContextReturn: captureRecordContextOrigin(location, event.currentTarget) } })
  }
  const q = useQuery({ queryKey: ['project-peek', id], queryFn: () => typedRequest('get', '/api/v1/projects/{id}/peek', { params: { id } }),
    staleTime: 0, gcTime: 0, refetchOnMount: 'always' })
  const data = !q.isFetching ? q.data : undefined
  return <Dialog open onOpenChange={open => !open && close()}><DialogContent className="max-h-[85dvh] overflow-y-auto sm:max-w-2xl">
    <DialogHeader><DialogTitle>{t('projectScreens.peek')}</DialogTitle><DialogDescription>{data ? `${data.projectNumber} · ${data.name}` : t('projectScreens.peekHint')}</DialogDescription></DialogHeader>
    {q.error ? <ErrorBanner error={q.error} retry={() => q.refetch()} /> : !data ? <Loading rows={5} /> : <div className="space-y-5 text-sm">
      <section className="space-y-2"><h3 className="font-medium">{t('projectScreens.nextMilestones')}</h3>
        {!data.milestones.length ? <Empty>{t('projectScreens.noOpenMilestones')}</Empty> : <ol className="divide-y rounded border">{data.milestones.map(m => <li key={m.id} className="flex flex-wrap items-center gap-2 px-3 py-2">
          <Link className="min-w-0 flex-1 underline underline-offset-2" to={`/projects/${encodeURIComponent(data.projectNumber)}/milestones?panel=Milestone:${m.id}`} onClick={followMilestone}><Key>{m.key}</Key> {m.name}</Link>
          <span className="tabular-nums text-muted-foreground">{m.date ? fmtDate(m.date) : t('recordContext.noDate')}</span>{m.status ? <StatusPill status={m.status} /> : <span className="text-muted-foreground">{t('recordContext.notAssessed')}</span>}
        </li>)}</ol>}
      </section>
      <section className="space-y-2"><h3 className="font-medium">{t('projectScreens.disciplineLeads')}</h3>
        {!data.disciplines.length ? <Missing /> : <dl className="divide-y rounded border">{data.disciplines.map(d => <div key={d.id} className="flex flex-wrap items-start justify-between gap-2 px-3 py-2">
          <dt>{d.name}</dt><dd className="text-right"><span>{d.lead.name ?? t(d.lead.state === 'Unassigned' ? 'recordContext.unassigned' : 'recordContext.unknown')}{d.lead.state === 'Inactive' && ` · ${t('recordContext.inactive')}`}</span>
            <span className="block text-xs text-muted-foreground">{t('projectScreens.disciplineOverdue', { n: d.overdueTasks })}</span></dd>
        </div>)}</dl>}
      </section>
      <section className="space-y-2"><h3 className="font-medium">{t('statusNote.title')}</h3>{!data.statusNoteEnabled ? <p className="text-muted-foreground">{t('projectScreens.noteDisabled')}</p> : !data.statusNote ? <p className="text-muted-foreground">{t('projectScreens.noNote')}</p> : <div className="rounded border p-3"><p className="whitespace-pre-wrap break-words">{data.statusNote.text}</p><p className="mt-2 text-xs text-muted-foreground">{data.statusNote.author ?? t('recordContext.unknown')} · {data.statusNote.at ? fmtTime(data.statusNote.at) : t('recordContext.unknown')}</p></div>}</section>
      <div className="flex flex-wrap items-center justify-between gap-2 border-t pt-3"><span className="text-xs text-muted-foreground">{t('projectScreens.peekAsOf', { at: fmtTime(data.asOf) })}</span>
        <Button asChild variant="outline" size="sm"><Link to={`/projects/${encodeURIComponent(data.projectNumber)}`}>{t('projectScreens.openProject')}</Link></Button></div>
    </div>}
  </DialogContent></Dialog>
}
