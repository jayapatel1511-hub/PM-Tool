import { useQuery } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router'
import { ErrorBanner, Loading, Page } from '@/components/hub/common'
import { ViewMenu } from '@/components/hub/views'
import { useScope } from '@/components/hub/workspace'
import { get, qs } from '@/lib/api'
import { csvCell } from '@/lib/csv'
import { plural, t } from '@/lib/i18n'
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
  const blob = new Blob([`\ufeff${lines.join('\n')}\n`], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url; anchor.download = 'discipline-coordination.csv'; document.body.append(anchor); anchor.click(); anchor.remove()
  setTimeout(() => URL.revokeObjectURL(url), 0)
}

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
  if (q.isPending) return <Page title={t('dcv.ws.title')}><Loading rows={5} /></Page>
  if (q.error) return <Page title={t('dcv.ws.title')}><div className="flex flex-wrap gap-3 rounded border p-3">
    <label>{t('common.from')}<input className="ml-2 rounded border p-1" type="date" value={from} onChange={e => set('from', e.target.value)} /></label>
    <label>{t('common.to')}<input className="ml-2 rounded border p-1" type="date" value={to} onChange={e => set('to', e.target.value)} /></label>
    <button type="button" className="text-primary underline" onClick={() => setSp(new URLSearchParams(), { replace: true })}>{t('dcv.ws.clearFilters')}</button>
  </div><ErrorBanner error={q.error} retry={() => q.refetch()} /></Page>
  const data = q.data
  const item = (project: ProjectProjection, path: string, prefix: string, row: Item) =>
    <li key={row.id}><Link className="text-primary underline" to={`/projects/${project.projectNumber}/${path}?panel=${prefix}:${row.id}`}>
      {project.projectNumber} · {row.key}</Link> · {row.title} · {row.status}</li>
  const actionsFor = (project: ProjectProjection, type: LinkedAction['sourceType'], id: string) =>
    project.data.linkedActions.filter(action => action.sourceType === type && action.sourceId === id)
      .map(action => <li key={action.id}>{t('dcv.existingAction')} <Link className="text-primary underline" to={`/projects/${project.projectNumber}/meetings?panel=Action:${action.id}`}>
        {action.key}</Link> · {action.text} · {action.status}{action.dueDate && ` · ${t('dcv.actionDue', { date: action.dueDate })}`}</li>)
  return <Page title={t('dcv.ws.title')} subtitle={t('dcv.ws.subtitle')}
    actions={<ViewMenu listType="workspace-coordination" extra={() => ({ ...scope.params, tab: 'coordination' })} fixed={{ tab: 'coordination' }} />}>
    <p className="no-print flex gap-4"><button type="button" className="text-primary underline" onClick={() => downloadCsv(data)}>{t('dcv.ws.exportCsv')}</button>
      <button type="button" className="text-primary underline" onClick={() => window.print()}>{t('dcv.ws.printView')}</button></p>
    <div className="no-print flex flex-wrap gap-3 rounded border p-3">
      <span className="flex items-center gap-2"><label htmlFor="workspace-coordination-project">{t('common.project')}</label><select id="workspace-coordination-project" className="rounded border p-1" value={projectId} onChange={e => set('projectId', e.target.value)}>
        <option value="">{t('dcv.ws.allPermittedProjects')}</option>{data.projectChoices.map(p => <option key={p.id} value={p.id}>{p.projectNumber}</option>)}
      </select></span>
      <span className="flex items-center gap-2"><label htmlFor="workspace-coordination-discipline">{t('common.discipline')}</label><select id="workspace-coordination-discipline" className="rounded border p-1" value={disciplineId} onChange={e => set('disciplineId', e.target.value)}>
        <option value="">{t('dcv.ws.myProjectDisciplines')}</option>{data.disciplines.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
      </select></span>
      <span className="flex items-center gap-2"><label htmlFor="workspace-coordination-owner">{t('common.owner')}</label><select id="workspace-coordination-owner" className="rounded border p-1" value={ownerId} onChange={e => set('ownerId', e.target.value)}>
        <option value="">{t('dcv.ws.allOwners')}</option>{data.owners.map(o => <option key={o.id} value={o.id}>{o.displayName}</option>)}
      </select></span>
      <span className="flex items-center gap-2"><label htmlFor="workspace-coordination-from">{t('common.from')}</label><input id="workspace-coordination-from" className="rounded border p-1" type="date" value={from} onChange={e => set('from', e.target.value)} /></span>
      <span className="flex items-center gap-2"><label htmlFor="workspace-coordination-to">{t('common.to')}</label><input id="workspace-coordination-to" className="rounded border p-1" type="date" value={to} onChange={e => set('to', e.target.value)} /></span>
    </div>
    <p role="status" className="text-xs text-muted-foreground">{t('dcv.ws.evaluated', { when: new Date(data.evaluatedAt).toLocaleString(), n: data.projects.length })}</p>
    {data.projects.map(project => <section key={project.id} className="space-y-3 rounded border p-4" aria-labelledby={`coord-${project.id}`}>
      <h2 id={`coord-${project.id}`} className="font-semibold">{project.projectNumber} · {project.name}</h2>
      <div className="grid gap-3 md:grid-cols-2">
        <section><h3>{t('dcv.owe')} ({project.data.outgoing.length})</h3>
          <ul>{project.data.outgoing.map(h => item(project, 'handoffs', 'Handoff', h))}</ul></section>
        <section><h3>{t('dcv.waiting')} ({project.data.incoming.length})</h3>
          <ul>{project.data.incoming.map(h => item(project, 'handoffs', 'Handoff', h))}</ul>
          {project.data.blockerGroups.length > 0 && <h4>{t('dcv.ws.linkedTaskBlockers')}</h4>}<ul>{project.data.blockerGroups.map(g =>
          <li key={g.handoffId}><Link className="text-primary underline" to={`/projects/${project.projectNumber}/handoffs?panel=Handoff:${g.handoffId}`}>{g.handoffKey}</Link>
            {' · '}<Link className="text-primary underline" to={`/projects/${project.projectNumber}/tasks?ids=${g.taskIds.join(',')}`}>{t('dcv.linkedTasks', { n: g.taskIds.length })}</Link>: {g.taskIds.map((id, index) => <span key={id}>{index > 0 && ', '}
              <Link className="text-primary underline" to={`/projects/${project.projectNumber}/tasks?panel=Task:${id}`}>{g.taskKeys[index]}</Link></span>)}
            <ul>{actionsFor(project, 'Handoff', g.handoffId)}</ul></li>)}</ul></section>
        <section><h3>{t('dcv.using')} ({project.data.uses.length})</h3>
          <Link className="text-primary underline" to={`/projects/${project.projectNumber}/coordination`}>{t('dcv.ws.openSourceRevisions')}</Link></section>
        <section><h3>{t('dcv.changed')} ({project.data.changes.length})</h3><ul>{project.data.changes.map(c => <li key={c.id}>
          <Link className="text-primary underline" to={`/projects/${project.projectNumber}/changes?panel=ChangeNotice:${c.id}`}>
            {project.projectNumber} · {c.key}</Link> · {c.title} · {c.status} · <ChangeAssessmentCounts c={c} scoped={!!(project.disciplineId || ownerId)} />
          <ul>{actionsFor(project, 'ChangeNotice', c.id)}</ul>
          {project.data.unavailableChangeTargets.filter(target => target.changeNoticeId === c.id).map(target =>
            <p key={target.changeNoticeId} role="status">{plural(target.count, 'dcv.targetUnavailableOne', 'dcv.targetUnavailableMany')}</p>)}
        </li>)}</ul></section>
        <section><h3>{t('dcv.start')} ({t('dcv.readyOfAssessed', { ready: project.data.startabilityReadyTotal, n: project.data.startability.length })})</h3>
          <p className="text-xs text-muted-foreground">{t('dcv.ws.assessedWindow', { from: project.data.startabilityFrom, to: project.data.startabilityTo })}</p>
          <ul>{project.data.startability.map(row => <li key={row.id}>
            <Link className="text-primary underline" to={`/projects/${project.projectNumber}/${row.targetType === 'Task' ? 'tasks' : 'deliverables'}?panel=${row.targetType}:${row.targetId}`}>
              {project.projectNumber} · {row.key}</Link> · {row.name} · {row.state}
            {row.blocked.length > 0 && <span> · {t('dcv.blocked', { list: row.blocked.join(', ') })}</span>}
            {row.unknown.length > 0 && <span> · {t('dcv.unknown', { list: row.unknown.join(', ') })}</span>}
          </li>)}</ul>
          {project.data.startability.length === 0 && <p>{t('dcv.noAssessedWork')}</p>}
          <Link className="text-primary underline" to={`/projects/${project.projectNumber}/readiness`}>{t('dcv.ws.openReadiness')}</Link></section>
      </div>
      {project.data.reviews.length > 0 && <p>{t('dcv.ws.reviewPackages', { n: project.data.reviews.length })} · <Link className="text-primary underline" to={`/projects/${project.projectNumber}/reviews`}>{t('dcv.ws.openReviews')}</Link></p>}
      {project.data.linkedIssues.length > 0 && <p>{t('dcv.ws.linkedIssues', { n: project.data.linkedIssues.length })} · <Link className="text-primary underline" to={`/projects/${project.projectNumber}/issues`}>{t('dcv.ws.openIssues')}</Link></p>}
      <p><Link className="text-primary underline" to={`/projects/${project.projectNumber}/coordination${qs({ meeting: '1', discipline: project.disciplineId, owner: ownerId, from, to })}`}>
        {t('dcv.ws.openMeetingMode')}</Link></p>
    </section>)}
    {data.projects.length === 0 && <p>{t('dcv.ws.noProjects')}</p>}
  </Page>
}
