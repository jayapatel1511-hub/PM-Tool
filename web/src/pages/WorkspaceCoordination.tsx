import { WorkRowEvidence } from '@/components/hub/work-row-evidence'
import { personOptionLabel } from '@/components/hub/person-option-label'
import { useQuery } from '@tanstack/react-query'
import { Download, Printer } from 'lucide-react'
import type { ComponentProps, ReactNode } from 'react'
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router'
import { ActiveFilters, Empty, ErrorBanner, Field, FilterBar, Loading, Missing, Page, Section, selectCls } from '@/components/hub/common'
import { ViewMenu } from '@/components/hub/views'
import { useScope } from '@/components/hub/workspace'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import type { components } from '@/gen/api'
import { qs, typedRequest } from '@/lib/api'
import { csvCell } from '@/lib/csv'
import { fmtDate } from '@/lib/format'
import { plural, t, tv } from '@/lib/i18n'
import { accentOf, cn } from '@/lib/utils'
import { ChangeAssessmentCounts } from './projects/DisciplineCoordinationView'
import { RecordContext } from '@/components/hub/record-context'
import { useItemPanel } from '@/components/hub/panel-host'
import { useFlag, useMe } from '@/lib/auth'
import { captureRecordContextOrigin } from '@/lib/record-context-navigation'

type Schemas = components['schemas']
type Item = { id: string; key: string; title: string; status: string; context?: Schemas['RecordContextView'] | null }
type Projection = Schemas['WorkspaceCoordinationProjection']
type ProjectProjection = Projection['projects'][number]

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

function ContextLink({ to, children, ...props }: Omit<ComponentProps<typeof Link>, 'to'> & { to: string }) {
  const location = useLocation(), navigate = useNavigate(), contextOn = useFlag('screen_patterns')
  return <Link to={to} {...props} onClick={event => {
    props.onClick?.(event)
    if (!contextOn || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return
    event.preventDefault()
    navigate(to, { state: { recordContextReturn: captureRecordContextOrigin(location, event.currentTarget) } })
  }}>{children}</Link>
}

/** Permitted workspace projection. One server snapshot supplies each project's rows and counts. */
export function WorkspaceCoordination() {
  const noticesOn = useFlag('capacity_screens')
  const me = useMe(), contextOn = useFlag('screen_patterns'), workOn = useFlag('work_screens'), peopleOn = useFlag('people_screens')
  const openPanel = useItemPanel()
  const context = (value: Schemas['RecordContextView'] | null | undefined, projectNumber: string) => contextOn && value ? <RecordContext context={value} projectNumber={projectNumber} meId={me.id} openPanel={openPanel} /> : null
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
  const q = useQuery({ queryKey: ['workspace-coordination', scope.kind, scope.api, projectId, disciplineId, ownerId, from, to, me.id, workOn, peopleOn],
    enabled: scope.ready,
    queryFn: () => typedRequest('get', '/api/v1/discipline-coordination', { query: { ...scopeParams, projectId, disciplineId, ownerId, from, to } }) })
  const header = { title: t('dcv.ws.title'), subtitle: t('dcv.ws.subtitle') }
  const dates = <>
    <Field label={t('common.from')} htmlFor="workspace-coordination-from" className="w-full sm:w-44"><Input id="workspace-coordination-from" type="date" value={from} onChange={e => set('from', e.target.value)} /></Field>
    <Field label={t('common.to')} htmlFor="workspace-coordination-to" className="w-full sm:w-44"><Input id="workspace-coordination-to" type="date" value={to} onChange={e => set('to', e.target.value)} /></Field>
  </>
  if (q.isPending) return <Page {...header}><div className="rounded-lg border bg-card"><Loading rows={5} /></div></Page>
  if (q.error) return <Page {...header}><FilterBar activeCount={[from, to].filter(Boolean).length}><div className="flex flex-wrap items-end gap-3">{dates}
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
  const evidence = (project: ProjectProjection, value: object, type: string, id: string, key: string, section: string) => <WorkRowEvidence row={value} itemType={type} itemId={id} itemKey={key} projectNumber={project.projectNumber} sourceKey={`workspace-coordination:${project.id}:${section}:${type}:${id}`} />
  const none = <p className="text-muted-foreground">{t('dcv.none')}</p>
  const item = (project: ProjectProjection, path: string, prefix: string, row: Item, section = path) =>
    <li key={row.id}>{context(row.context, project.projectNumber)}<ContextLink className={cn('font-medium', link)} to={`/projects/${project.projectNumber}/${path}?panel=${prefix}:${row.id}`}>
      {project.projectNumber} · {row.key}</ContextLink> · {row.title} · <span className="text-muted-foreground">{tv(row.status)}</span>{evidence(project, row, prefix, row.id, row.key, section)}</li>
  const actionsFor = (project: ProjectProjection, type: string, id: string) =>
    project.data.linkedActions.filter(action => action.sourceType === type && action.sourceId === id)
      .map(action => <li key={action.id}>{context(action.context, project.projectNumber)}{t('dcv.existingAction')} <ContextLink className={link} to={`/projects/${project.projectNumber}/meetings?panel=Action:${action.id}`}>
        {action.key}</ContextLink> · {action.text} · <span className="text-muted-foreground">{tv(action.status)}</span>{action.dueDate && ` · ${t('dcv.actionDue', { date: fmtDate(action.dueDate) })}`}{evidence(project, action, 'Action', action.id, action.key, `linked:${action.sourceType}:${action.sourceId}`)}</li>)
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
    <FilterBar className="no-print" activeCount={tokens.length}
      active={<ActiveFilters tokens={tokens.map(([key, label, value]) => ({ key, label, value: value || <Missing /> }))} onRemove={name => set(name, '')} onClear={clear} />}>
      <div className="flex flex-wrap items-end gap-3">
        <Field label={t('common.project')} htmlFor="workspace-coordination-project" className="w-full sm:w-52"><select id="workspace-coordination-project" className={selectCls} value={projectId} onChange={e => set('projectId', e.target.value)}>
          <option value="">{t('dcv.ws.allPermittedProjects')}</option>{data.projectChoices.map(p => <option key={p.id} value={p.id}>{p.projectNumber}</option>)}
        </select></Field>
        <Field label={t('common.discipline')} htmlFor="workspace-coordination-discipline" className="w-full sm:w-56"><select id="workspace-coordination-discipline" className={selectCls} value={disciplineId} onChange={e => set('disciplineId', e.target.value)}>
          <option value="">{t('dcv.ws.myProjectDisciplines')}</option>{data.disciplines.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
        </select></Field>
        <Field label={t('common.owner')} htmlFor="workspace-coordination-owner" className="w-full sm:w-52"><select id="workspace-coordination-owner" className={selectCls} value={ownerId} onChange={e => set('ownerId', e.target.value)}>
          <option value="">{t('dcv.ws.allOwners')}</option>{data.owners.map(o => <option key={o.id} value={o.id}>{personOptionLabel(o.displayName, o.personNotices, noticesOn)}</option>)}
        </select></Field>
        {dates}
      </div>
    </FilterBar>
    <p role="status" className="text-xs/[18px] text-muted-foreground">{t('dcv.ws.evaluated', { when: new Date(data.evaluatedAt).toLocaleString(), n: data.projects.length })}</p>
    {data.projects.map(project => <Section key={project.id} id={`coord-${project.id}`} accent={accentOf(project.id)}
      title={<><span className="key mr-2 text-(--acc-fg)">{project.projectNumber}</span>{project.name}</>}>
      <div className="grid gap-x-8 gap-y-6 p-5 md:grid-cols-2">
        {block(t('dcv.owe'), project.data.outgoing.length,
          project.data.outgoing.length ? <ul className="space-y-1.5">{project.data.outgoing.map(h => item(project, 'handoffs', 'Handoff', h, 'outgoing'))}</ul> : none)}
        {block(t('dcv.waiting'), project.data.incoming.length, <>
          {project.data.incoming.length ? <ul className="space-y-1.5">{project.data.incoming.map(h => item(project, 'handoffs', 'Handoff', h, 'incoming'))}</ul> : none}
          {project.data.blockerGroups.length > 0 && <>
            <h4 className="pt-1 font-medium">{t('dcv.ws.linkedTaskBlockers')}</h4>
            <ul className="space-y-1.5">{project.data.blockerGroups.map(g =>
              <li key={g.handoffId}>{context(g.context, project.projectNumber)}<ContextLink className={cn('font-medium', link)} to={`/projects/${project.projectNumber}/handoffs?panel=Handoff:${g.handoffId}`}>{g.handoffKey}</ContextLink>
                {' · '}<Link className={link} to={`/projects/${project.projectNumber}/tasks?ids=${g.taskIds.join(',')}`}>{t('dcv.linkedTasks', { n: g.taskIds.length })}</Link>: {g.taskIds.map((id, index) => <span key={id}>{index > 0 && ', '}
                  <ContextLink className={link} to={`/projects/${project.projectNumber}/tasks?panel=Task:${id}`}>{g.taskKeys[index]}</ContextLink></span>)}
                {evidence(project, g, 'Handoff', g.handoffId, g.handoffKey, 'blockers')}<ul className="mt-1 space-y-1 pl-4">{actionsFor(project, 'Handoff', g.handoffId)}</ul></li>)}</ul>
          </>}
        </>)}
        {block(t('dcv.using'), project.data.uses.length, <>
          {contextOn && project.data.uses.length > 0 && <ul className="space-y-3">{project.data.uses.map(use => <li key={use.id}>
            {context(use.context, project.projectNumber)}
            {use.targetKey && (use.targetType === 'Task' || use.targetType === 'Deliverable') ? <ContextLink className={link} to={`/projects/${project.projectNumber}/${use.targetType === 'Task' ? 'tasks' : 'deliverables'}?panel=${use.targetType}:${use.targetId}`}>{use.targetKey}</ContextLink> : <Missing />} · {use.targetName ?? t('dcv.unknownWork')}
            {use.targetKey && evidence(project, use, use.targetType, use.targetId, use.targetKey, `use:${use.id}`)}
            <p>{t('dcv.sourceRevision')}: {use.sourceUrl ? <a className={link} href={use.sourceUrl} target="_blank" rel="noopener noreferrer">{use.sourceKey} {use.revision}</a> : <Missing />}</p>
          </li>)}</ul>}
          <Link className={link} to={`/projects/${project.projectNumber}/coordination`}>{t('dcv.ws.openSourceRevisions')}</Link>
        </>)}
        {block(t('dcv.changed'), project.data.changes.length,
          project.data.changes.length ? <ul className="space-y-2">{project.data.changes.map(c => <li key={c.id}>
            {context(c.context, project.projectNumber)}<ContextLink className={cn('font-medium', link)} to={`/projects/${project.projectNumber}/changes?panel=ChangeNotice:${c.id}`}>
              {project.projectNumber} · {c.key}</ContextLink> · {c.title} · <span className="text-muted-foreground">{tv(c.status)}</span> · <ChangeAssessmentCounts c={c} scoped={!!(project.disciplineId || ownerId)} />
            {evidence(project, c, 'ChangeNotice', c.id, c.key, 'changes')}<ul className="mt-1 space-y-1 pl-4">{actionsFor(project, 'ChangeNotice', c.id)}</ul>
            {project.data.unavailableChangeTargets.filter(target => target.changeNoticeId === c.id).map(target =>
              <p key={target.changeNoticeId} role="status" className="text-xs/[18px] text-muted-foreground">{plural(target.count, 'dcv.targetUnavailableOne', 'dcv.targetUnavailableMany')}</p>)}
          </li>)}</ul> : none)}
        {block(t('dcv.start'), t('dcv.readyOfAssessed', { ready: project.data.startabilityReadyTotal, n: project.data.startability.length }), <>
          <p className="text-xs/[18px] text-muted-foreground">{t('dcv.ws.assessedWindow', { from: project.data.startabilityFrom, to: project.data.startabilityTo })}</p>
          {project.data.startability.length > 0 && <ul className="space-y-1.5">{project.data.startability.map(row => <li key={row.id}>
            {context(row.context, project.projectNumber)}<ContextLink className={cn('font-medium', link)} to={`/projects/${project.projectNumber}/${row.targetType === 'Task' ? 'tasks' : 'deliverables'}?panel=${row.targetType}:${row.targetId}`}>
              {project.projectNumber} · {row.key}</ContextLink> · {row.name} · <span className="text-muted-foreground">{tv(row.state)}</span>
            {evidence(project, row, row.targetType, row.targetId, row.key, 'startability')}
            {row.blocked.length > 0 && <span className="text-bad"> · <span aria-hidden>■ </span>{t('dcv.blocked', { list: row.blocked.join(', ') })}</span>}
            {row.unknown.length > 0 && <span className="text-warn"> · <span aria-hidden>▲ </span>{t('dcv.unknown', { list: row.unknown.join(', ') })}</span>}
          </li>)}</ul>}
          {project.data.startability.length === 0 && <p className="text-muted-foreground">{t('dcv.noAssessedWork')}</p>}
          <Link className={link} to={`/projects/${project.projectNumber}/readiness`}>{t('dcv.ws.openReadiness')}</Link>
        </>)}
      </div>
      <div className="flex flex-col gap-2 border-t px-5 py-3 text-sm">
        {project.data.reviews.length > 0 && <div><p>{t('dcv.ws.reviewPackages', { n: project.data.reviews.length })} · <Link className={link} to={`/projects/${project.projectNumber}/reviews`}>{t('dcv.ws.openReviews')}</Link></p>{contextOn && <ul className="mt-2 space-y-3">{project.data.reviews.map(row => item(project, 'reviews', 'ReviewPackage', row))}</ul>}</div>}
        {project.data.linkedIssues.length > 0 && <div><p>{t('dcv.ws.linkedIssues', { n: project.data.linkedIssues.length })} · <Link className={link} to={`/projects/${project.projectNumber}/issues`}>{t('dcv.ws.openIssues')}</Link></p>{contextOn && <ul className="mt-2 space-y-3">{project.data.linkedIssues.map(row => item(project, 'issues', 'Issue', row))}</ul>}</div>}
        {contextOn && project.data.upcomingSubmissions.length > 0 && <div><h3 className="font-medium">{t('dcv.upcomingSubmissions')}</h3><ul className="mt-2 space-y-3">{project.data.upcomingSubmissions.map(row => item(project, 'submissions', 'SubmissionPackage', { ...row, status: row.effectiveStatus }))}</ul></div>}
        {contextOn && project.data.staffingConflicts.length > 0 && <div><h3 className="font-medium">{t('dcv.staffingConflicts')}</h3><ul className="mt-2 space-y-3">{project.data.staffingConflicts.map(row => item(project, 'readiness', 'WorkConstraint', { ...row, title: row.description, status: row.state }))}</ul></div>}
        <p><Link className={cn('font-medium', link)} to={`/projects/${project.projectNumber}/coordination${qs({ meeting: '1', discipline: project.disciplineId, owner: ownerId, from, to })}`}>
          {t('dcv.ws.openMeetingMode')}</Link></p>
      </div>
    </Section>)}
    {data.projects.length === 0 && <div className="rounded-lg border bg-card">
      <Empty action={tokens.length > 0 && <Button variant="outline" onClick={clear}>{t('filters.clear')}</Button>}>{t('dcv.ws.noProjects')}</Empty></div>}
  </Page>
}
