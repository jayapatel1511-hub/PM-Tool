import { useQuery } from '@tanstack/react-query'
import { Download, Printer } from 'lucide-react'
import type { ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router'
import { ActiveFilters, Empty, ErrorBanner, Field, FilterBar, Loading, Missing, Page, Section, selectCls } from '@/components/hub/common'
import { ViewMenu } from '@/components/hub/views'
import { useScope } from '@/components/hub/workspace'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { get, qs } from '@/lib/api'
import { csvCell } from '@/lib/csv'
import { fmtDate } from '@/lib/format'
import { plural, t, tv } from '@/lib/i18n'
import { accentOf, cn } from '@/lib/utils'
import { ChangeAssessmentCounts, type ChangeCounts } from './projects/DisciplineCoordinationView'

type Item = { id: string; key: string; title: string; status: string }
type Handoff = Item & { sendingOwnerId: string; receivingOwnerId: string; sendingDisciplineId: string; receivingDisciplineId: string }
type Group = { handoffId: string; handoffKey: string; taskIds: string[]; taskKeys: string[] }
type Startability = { id: string; targetType: 'Task' | 'Deliverable'; targetId: string; key: string; name: string;
  dueDate: string | null; state: string; blocked: string[]; unknown: string[] }
type LinkedAction = { id: string; key: string; text: string; status: string; dueDate: string | null;
  sourceType: 'Handoff' | 'ChangeNotice'; sourceId: string }
type UnavailableChangeTarget = { changeNoticeId: string; count: number }
type ProjectProjection = { id: string; projectNumber: string; name: string; disciplineId: string | null; data: {
  handoffs: Handoff[]; outgoing: Handoff[]; incoming: Handoff[]; changes: (Item & ChangeCounts)[]; reviews: Item[];
  linkedIssues: Item[]; uses: { id: string; targetKey: string | null; targetName: string | null; sourceKey: string | null; revision: string | null; intendedUse: string }[]; blockerGroups: Group[];
  startability: Startability[]; startabilityReadyTotal: number; startabilityFrom: string; startabilityTo: string;
  linkedActions: LinkedAction[]; unavailableChangeTargets: UnavailableChangeTarget[]
} }
type Choice = { id: string; name?: string; projectNumber?: string; displayName?: string }
type Projection = { evaluatedAt: string; projects: ProjectProjection[]; projectChoices: Choice[];
  disciplines: Choice[]; owners: Choice[] }

function downloadCsv(projection: Projection) {
  const lines = ['evaluatedAt,project,section,id,key,title,status']
  const add = (number: string, section: string, id: string, key: string, title: string, status: string) =>
    lines.push([projection.evaluatedAt, number, section, id, key, title, status].map(csvCell).join(','))
  for (const project of projection.projects) {
    const number = project.projectNumber, rows = project.data
    for (const row of rows.outgoing) add(number, 'Outgoing Handoff', row.id, row.key, row.title, row.status)
    for (const row of rows.incoming) add(number, 'Incoming Handoff', row.id, row.key, row.title, row.status)
    for (const row of rows.changes) add(number, 'Change', row.id, row.key,
      `${row.title} · pending: ${row.pendingAssessments} · acknowledged: ${row.acknowledgedPending} · project pending: ${row.projectPending}`, row.status)
    for (const row of rows.reviews) add(number, 'Review', row.id, row.key, row.title, row.status)
    for (const row of rows.linkedIssues) add(number, 'Issue', row.id, row.key, row.title, row.status)
    for (const row of rows.uses) add(number, 'InputUse', row.id, row.targetKey ?? '',
      `${row.targetName ?? ''} · ${row.sourceKey ?? ''} rev ${row.revision ?? ''} · ${row.intendedUse}`, '')
    for (const group of rows.blockerGroups) add(number, 'BlockerGroup', group.handoffId, group.handoffKey, group.taskKeys.join('; '), '')
    for (const row of rows.startability) add(number, 'Startability', row.targetId, row.key,
      `${row.name}${row.blocked.length ? ` · blocked: ${row.blocked.join('; ')}` : ''}${row.unknown.length ? ` · unknown: ${row.unknown.join('; ')}` : ''}`, row.state)
    for (const row of rows.linkedActions) add(number, 'Linked Action', row.id, row.key,
      `${row.text} · ${row.sourceType}:${row.sourceId}`, row.status)
    for (const row of rows.unavailableChangeTargets) add(number, 'Unavailable Assessment Targets', row.changeNoticeId,
      '', String(row.count), '')
  }
  const blob = new Blob([`﻿${lines.join('\n')}\n`], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url; anchor.download = 'discipline-coordination.csv'; document.body.append(anchor); anchor.click(); anchor.remove()
  setTimeout(() => URL.revokeObjectURL(url), 0)
}

const link = 'text-primary underline underline-offset-4 hover:text-foreground'

/** Permitted workspace projection. One server snapshot supplies each project's rows and counts. */
export function WorkspaceCoordination() {
  const [sp, setSp] = useSearchParams()
  const scope = useScope()
  const projectId = sp.get('projectId') ?? '', disciplineId = sp.get('disciplineId') ?? ''
  const ownerId = sp.get('ownerId') ?? '', from = sp.get('from') ?? '', to = sp.get('to') ?? ''
  const set = (name: string, value: string) => setSp(previous => {
    const next = new URLSearchParams(previous)
    if (value) next.set(name, value); else next.delete(name)
    return next
  }, { replace: true })
  const scopeParams = { scopeKind: scope.kind, scopeProjectIds: scope.kind === 'set' ? scope.ids?.join(',') : undefined,
    scopeWorkspaceId: scope.ws?.id }
  const q = useQuery({ queryKey: ['workspace-coordination', scope.kind, scope.api, projectId, disciplineId, ownerId, from, to],
    enabled: scope.ready,
    queryFn: () => get<Projection>(`discipline-coordination${qs({ ...scopeParams, projectId, disciplineId, ownerId, from, to })}`) })
  const header = { title: t('dcv.ws.title'), subtitle: t('dcv.ws.subtitle') }
  const dates = <>
    <Field label={t('common.from')} htmlFor="workspace-coordination-from" className="w-full sm:w-44"><Input id="workspace-coordination-from" type="date" value={from} onChange={e => set('from', e.target.value)} /></Field>
    <Field label={t('common.to')} htmlFor="workspace-coordination-to" className="w-full sm:w-44"><Input id="workspace-coordination-to" type="date" value={to} onChange={e => set('to', e.target.value)} /></Field>
  </>
  if (q.isPending) return <Page {...header}><div className="rounded-lg border bg-card"><Loading rows={5} /></div></Page>
  if (q.error) return <Page {...header}><FilterBar><div className="flex flex-wrap items-end gap-3">{dates}
    <Button variant="link" onClick={() => setSp(new URLSearchParams(), { replace: true })}>{t('dcv.ws.clearFilters')}</Button>
  </div></FilterBar><ErrorBanner error={q.error} retry={() => q.refetch()} /></Page>
  const data = q.data
  const tokens = ([
    ['projectId', t('common.project'), data.projectChoices.find(p => p.id === projectId)?.projectNumber],
    ['disciplineId', t('common.discipline'), data.disciplines.find(d => d.id === disciplineId)?.name],
    ['ownerId', t('common.owner'), data.owners.find(o => o.id === ownerId)?.displayName],
    ['from', t('common.from'), from && fmtDate(from)],
    ['to', t('common.to'), to && fmtDate(to)],
  ] as const).filter(([name]) => sp.get(name))
  const clear = () => setSp(previous => { const next = new URLSearchParams(previous); for (const [name] of tokens) next.delete(name); return next }, { replace: true })
  const none = <p className="text-muted-foreground">{t('dcv.none')}</p>
  const item = (project: ProjectProjection, path: string, prefix: string, row: Item) =>
    <li key={row.id}><Link className={cn('font-medium', link)} to={`/projects/${project.projectNumber}/${path}?panel=${prefix}:${row.id}`}>
      {project.projectNumber} · {row.key}</Link> · {row.title} · <span className="text-muted-foreground">{tv(row.status)}</span></li>
  const actionsFor = (project: ProjectProjection, type: LinkedAction['sourceType'], id: string) =>
    project.data.linkedActions.filter(action => action.sourceType === type && action.sourceId === id)
      .map(action => <li key={action.id}>{t('dcv.existingAction')} <Link className={link} to={`/projects/${project.projectNumber}/meetings?panel=Action:${action.id}`}>
        {action.key}</Link> · {action.text} · <span className="text-muted-foreground">{tv(action.status)}</span>{action.dueDate && ` · ${t('dcv.actionDue', { date: fmtDate(action.dueDate) })}`}</li>)
  /** One coordination question with its count, as in the project view's sectioned agenda. */
  const block = (title: string, count: ReactNode, body: ReactNode) => <section className="min-w-0 space-y-2">
    <h3 className="flex flex-wrap items-center gap-2 text-sm font-semibold">{title}
      <span className="rounded-md bg-secondary px-2 py-0.5 text-xs font-medium text-muted-foreground tabular-nums">{count}</span></h3>
    <div className="space-y-2 text-sm">{body}</div>
  </section>
  return <Page {...header}
    actions={<>
      <ViewMenu listType="workspace-coordination" extra={() => ({ ...scope.params, tab: 'coordination' })} fixed={{ tab: 'coordination' }} />
      <Button variant="outline" onClick={() => window.print()}><Printer className="size-4" />{t('dcv.ws.printView')}</Button>
      <Button variant="outline" onClick={() => downloadCsv(data)}><Download className="size-4" />{t('dcv.ws.exportCsv')}</Button>
    </>}>
    <FilterBar className="no-print">
      <div className="flex flex-wrap items-end gap-3">
        <Field label={t('common.project')} htmlFor="workspace-coordination-project" className="w-full sm:w-52"><select id="workspace-coordination-project" className={selectCls} value={projectId} onChange={e => set('projectId', e.target.value)}>
          <option value="">{t('dcv.ws.allPermittedProjects')}</option>{data.projectChoices.map(p => <option key={p.id} value={p.id}>{p.projectNumber}</option>)}
        </select></Field>
        <Field label={t('common.discipline')} htmlFor="workspace-coordination-discipline" className="w-full sm:w-56"><select id="workspace-coordination-discipline" className={selectCls} value={disciplineId} onChange={e => set('disciplineId', e.target.value)}>
          <option value="">{t('dcv.ws.myProjectDisciplines')}</option>{data.disciplines.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
        </select></Field>
        <Field label={t('common.owner')} htmlFor="workspace-coordination-owner" className="w-full sm:w-52"><select id="workspace-coordination-owner" className={selectCls} value={ownerId} onChange={e => set('ownerId', e.target.value)}>
          <option value="">{t('dcv.ws.allOwners')}</option>{data.owners.map(o => <option key={o.id} value={o.id}>{o.displayName}</option>)}
        </select></Field>
        {dates}
      </div>
      <ActiveFilters tokens={tokens.map(([key, label, value]) => ({ key, label, value: value || <Missing /> }))} onRemove={name => set(name, '')} onClear={clear} />
    </FilterBar>
    <p role="status" className="text-xs/[18px] text-muted-foreground">{t('dcv.ws.evaluated', { when: new Date(data.evaluatedAt).toLocaleString(), n: data.projects.length })}</p>
    {data.projects.map(project => <Section key={project.id} id={`coord-${project.id}`} accent={accentOf(project.id)}
      title={<><span className="key mr-2 text-(--acc-fg)">{project.projectNumber}</span>{project.name}</>}>
      <div className="grid gap-x-8 gap-y-6 p-5 md:grid-cols-2">
        {block(t('dcv.owe'), project.data.outgoing.length,
          project.data.outgoing.length ? <ul className="space-y-1.5">{project.data.outgoing.map(h => item(project, 'handoffs', 'Handoff', h))}</ul> : none)}
        {block(t('dcv.waiting'), project.data.incoming.length, <>
          {project.data.incoming.length ? <ul className="space-y-1.5">{project.data.incoming.map(h => item(project, 'handoffs', 'Handoff', h))}</ul> : none}
          {project.data.blockerGroups.length > 0 && <>
            <h4 className="pt-1 font-medium">{t('dcv.ws.linkedTaskBlockers')}</h4>
            <ul className="space-y-1.5">{project.data.blockerGroups.map(g =>
              <li key={g.handoffId}><Link className={cn('font-medium', link)} to={`/projects/${project.projectNumber}/handoffs?panel=Handoff:${g.handoffId}`}>{g.handoffKey}</Link>
                {' · '}<Link className={link} to={`/projects/${project.projectNumber}/tasks?ids=${g.taskIds.join(',')}`}>{t('dcv.linkedTasks', { n: g.taskIds.length })}</Link>: {g.taskIds.map((id, index) => <span key={id}>{index > 0 && ', '}
                  <Link className={link} to={`/projects/${project.projectNumber}/tasks?panel=Task:${id}`}>{g.taskKeys[index]}</Link></span>)}
                <ul className="mt-1 space-y-1 pl-4">{actionsFor(project, 'Handoff', g.handoffId)}</ul></li>)}</ul>
          </>}
        </>)}
        {block(t('dcv.using'), project.data.uses.length,
          <Link className={link} to={`/projects/${project.projectNumber}/coordination`}>{t('dcv.ws.openSourceRevisions')}</Link>)}
        {block(t('dcv.changed'), project.data.changes.length,
          project.data.changes.length ? <ul className="space-y-2">{project.data.changes.map(c => <li key={c.id}>
            <Link className={cn('font-medium', link)} to={`/projects/${project.projectNumber}/changes?panel=ChangeNotice:${c.id}`}>
              {project.projectNumber} · {c.key}</Link> · {c.title} · <span className="text-muted-foreground">{tv(c.status)}</span> · <ChangeAssessmentCounts c={c} scoped={!!(project.disciplineId || ownerId)} />
            <ul className="mt-1 space-y-1 pl-4">{actionsFor(project, 'ChangeNotice', c.id)}</ul>
            {project.data.unavailableChangeTargets.filter(target => target.changeNoticeId === c.id).map(target =>
              <p key={target.changeNoticeId} role="status" className="text-xs/[18px] text-muted-foreground">{plural(target.count, 'dcv.targetUnavailableOne', 'dcv.targetUnavailableMany')}</p>)}
          </li>)}</ul> : none)}
        {block(t('dcv.start'), t('dcv.readyOfAssessed', { ready: project.data.startabilityReadyTotal, n: project.data.startability.length }), <>
          <p className="text-xs/[18px] text-muted-foreground">{t('dcv.ws.assessedWindow', { from: project.data.startabilityFrom, to: project.data.startabilityTo })}</p>
          {project.data.startability.length > 0 && <ul className="space-y-1.5">{project.data.startability.map(row => <li key={row.id}>
            <Link className={cn('font-medium', link)} to={`/projects/${project.projectNumber}/${row.targetType === 'Task' ? 'tasks' : 'deliverables'}?panel=${row.targetType}:${row.targetId}`}>
              {project.projectNumber} · {row.key}</Link> · {row.name} · <span className="text-muted-foreground">{tv(row.state)}</span>
            {row.blocked.length > 0 && <span className="text-bad"> · <span aria-hidden>■ </span>{t('dcv.blocked', { list: row.blocked.join(', ') })}</span>}
            {row.unknown.length > 0 && <span className="text-warn"> · <span aria-hidden>▲ </span>{t('dcv.unknown', { list: row.unknown.join(', ') })}</span>}
          </li>)}</ul>}
          {project.data.startability.length === 0 && <p className="text-muted-foreground">{t('dcv.noAssessedWork')}</p>}
          <Link className={link} to={`/projects/${project.projectNumber}/readiness`}>{t('dcv.ws.openReadiness')}</Link>
        </>)}
      </div>
      <div className="flex flex-col gap-2 border-t px-5 py-3 text-sm">
        {project.data.reviews.length > 0 && <p>{t('dcv.ws.reviewPackages', { n: project.data.reviews.length })} · <Link className={link} to={`/projects/${project.projectNumber}/reviews`}>{t('dcv.ws.openReviews')}</Link></p>}
        {project.data.linkedIssues.length > 0 && <p>{t('dcv.ws.linkedIssues', { n: project.data.linkedIssues.length })} · <Link className={link} to={`/projects/${project.projectNumber}/issues`}>{t('dcv.ws.openIssues')}</Link></p>}
        <p><Link className={cn('font-medium', link)} to={`/projects/${project.projectNumber}/coordination${qs({ meeting: '1', discipline: project.disciplineId, owner: ownerId, from, to })}`}>
          {t('dcv.ws.openMeetingMode')}</Link></p>
      </div>
    </Section>)}
    {data.projects.length === 0 && <div className="rounded-lg border bg-card">
      <Empty action={tokens.length > 0 && <Button variant="outline" onClick={clear}>{t('filters.clear')}</Button>}>{t('dcv.ws.noProjects')}</Empty></div>}
  </Page>
}
